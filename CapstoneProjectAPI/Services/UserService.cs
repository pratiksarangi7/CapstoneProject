using System.Text;
using AutoMapper;
using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Exceptions;
using CapstoneProjectAPI.Interfaces;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapstoneProjectAPI.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<UserService> _logger;

        public UserService(AppDbContext context, IMapper mapper, ILogger<UserService> logger)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<UserDetailsResponseDto> GetUserDetailsAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Department)
                .Include(u => u.Manager)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                _logger.LogWarning("Get user details failed: User with ID {UserId} was not found.", userId);
                throw new EntityNotFoundException($"User with ID {userId} was not found.");
            }

            return _mapper.Map<UserDetailsResponseDto>(user);
        }

        public async Task ChangePasswordAsync(int userId, ChangePasswordRequestDto request)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Change password failed: User with ID {UserId} was not found.", userId);
                throw new EntityNotFoundException($"User with ID {userId} was not found.");
            }

            if (!VerifyPassword(request.OldPassword, user.PasswordHash))
            {
                _logger.LogWarning("Change password failed: Old password verification failed for user {UserId}.", userId);
                throw new UnauthorizedAccessException("Old password is incorrect.");
            }

            if (request.OldPassword == request.NewPassword)
            {
                _logger.LogWarning("Change password failed: New password matches old password for user {UserId}.", userId);
                throw new ArgumentException("New password must be different from the old password.");
            }

            user.PasswordHash = HashPassword(request.NewPassword);

            _context.AuditLogs.Add(new AuditLog
            {
                PerformedByUserId = userId,
                Action = AuditAction.PasswordChanged,
                Details = $"User '{user.Name}' changed their password.",
                CreatedAt = DateTimeOffset.UtcNow
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} ('{Name}') successfully changed their password.", userId, user.Name);
        }

        public async Task<List<OnlyDepartmentResponseDto>> GetDepartmentsAsync()
        {
            var departments = await _context.Departments
                .OrderBy(d => d.Name)
                .ToListAsync();

            return departments.Select(d => new OnlyDepartmentResponseDto
            {
                Id = d.Id,
                Name = d.Name
            }).ToList();
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
    }
}