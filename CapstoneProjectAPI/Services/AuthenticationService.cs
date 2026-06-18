using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutoMapper;
using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Interfaces;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using CapstoneProjectAPI.Models.Enums;
using Microsoft.Extensions.Logging;

namespace CapstoneProjectAPI.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly ILogger<AuthenticationService> _logger;

        public AuthenticationService(AppDbContext context, IConfiguration configuration, IMapper mapper, ILogger<AuthenticationService> logger)
        {
            _context = context;
            _configuration = configuration;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<RegisterResponseDto> Register(RegisterRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                _logger.LogWarning("Registration failed: Email is missing.");
                throw new ArgumentException("Email is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                _logger.LogWarning("Registration failed for email {Email}: Password is missing.", request.Email);
                throw new ArgumentException("Password is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                _logger.LogWarning("Registration failed for email {Email}: Name is missing.", request.Email);
                throw new ArgumentException("Name is required.");
            }

            if (request.DepartmentId <= 0)
            {
                _logger.LogWarning("Registration failed for email {Email}: Department ID {DeptId} is invalid.", request.Email, request.DepartmentId);
                throw new ArgumentException("A valid Department ID is required.");
            }
            bool emailExists = await _context.Users
                .AnyAsync(u => u.Email == request.Email);

            if (emailExists)
            {
                _logger.LogWarning("Registration failed: User with email '{Email}' already exists.", request.Email);
                throw new InvalidOperationException($"A user with email '{request.Email}' already exists.");
            }

            string passwordHash = HashPassword(request.Password);

            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                PasswordHash = passwordHash,
                DepartmentId = request.DepartmentId,
                IsActive = false,
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _context.AuditLogs.Add(new AuditLog()
            {
                Action = AuditAction.UserRegistered,
                CreatedAt = DateTimeOffset.UtcNow,
                PerformedByUserId = user.Id,
                Details = $"New user registered. Email: {user.Email}",
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully registered new user {UserId} ('{Name}') with email {Email}.", user.Id, user.Name, user.Email);

            return _mapper.Map<RegisterResponseDto>(user);
        }

        public async Task<LoginResponseDto> Login(LoginRequestDto request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed: Invalid credentials for email '{Email}'.", request.Email);
                throw new UnauthorizedAccessException("Invalid email or password.");
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Login failed: Deactivated user '{Email}' attempted login.", request.Email);
                throw new UnauthorizedAccessException("Your account has been deactivated. Please contact your administrator.");
            }

            string token = GenerateJwtToken(user);
            _context.AuditLogs.Add(new AuditLog()
            {
                Action = AuditAction.UserRegistered,
                CreatedAt = DateTimeOffset.UtcNow,
                PerformedByUserId = user.Id,
                Details = $"User logged in. Email: {user.Email}",
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully authenticated user {UserId} ('{Email}'). JWT token generated.", user.Id, user.Email);

            return _mapper.Map<LoginResponseDto>(user, opts =>
            {
                opts.Items["JwtToken"] = token;
            });
        }


        private static string HashPassword(string password)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256();
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            string keyB64 = Convert.ToBase64String(hmac.Key);
            string hashB64 = Convert.ToBase64String(hash);
            return $"{keyB64}:{hashB64}";
        }

        private static bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                var parts = storedHash.Split(':');
                if (parts.Length != 2) return false;

                byte[] key = Convert.FromBase64String(parts[0]);
                byte[] expectedHash = Convert.FromBase64String(parts[1]);

                using var hmac = new System.Security.Cryptography.HMACSHA256(key);
                byte[] actualHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));

                return CryptographicEquals(expectedHash, actualHash);
            }
            catch
            {
                return false;
            }
        }

        private static bool CryptographicEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private string GenerateJwtToken(User user)
        {
            string role = user.IsAdmin ? "Admin" : "User";

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name,  user.Name),
                new Claim(ClaimTypes.Role,               role),
                new Claim("IsAdmin",                     user.IsAdmin.ToString()),
                new Claim("DepartmentId",                user.DepartmentId.ToString()),
                new Claim("ManagerId",                   user.ManagerId?.ToString() ?? ""),
                new Claim("Level",                       user.Level.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
            };

            var jwtSection = _configuration.GetSection("JWT");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            int minutes = jwtSection.GetValue<int>("DurationInMinutes");

            var token = new JwtSecurityToken(
                issuer: jwtSection["Issuer"],
                audience: jwtSection["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(minutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
