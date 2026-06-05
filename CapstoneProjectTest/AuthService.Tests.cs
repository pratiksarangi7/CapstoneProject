using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using AutoMapper;
using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Services;
using CapstoneProjectAPI.Mappings;

namespace CapstoneProjectTest
{
    public class AuthServiceTests
    {
        private AppDbContext _context;
        private IConfiguration _configuration;
        private IMapper _mapper;
        private AuthenticationService _authService;
        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "CapstoneAuthDb_" + Guid.NewGuid().ToString()) // Unique DB per run
                .Options;

            _context = new AppDbContext(options);
            _context.Departments.Add(new Department { Id = 1, Name = "Engineering" });
            _context.SaveChanges();
            var inMemorySettings = new Dictionary<string, string> {
                { "JWT:Key", "ThisIsASuperSecretKeyThatIsAtLeast32BytesLong" },
                { "JWT:Issuer", "CapstoneProjectAPI" },
                { "JWT:Audience", "CapstoneProjectAPI" },
                { "JWT:DurationInMinutes", "60" }
            };
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings!)
                .Build();
            var mappingConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
            _mapper = mappingConfig.CreateMapper();
            _authService = new AuthenticationService(_context, _configuration, _mapper);
        }
        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
        [Test]
        public async Task Register_ValidRequest_ReturnsResponseAndCreatesAuditLog()
        {
            var request = new RegisterRequestDto
            {
                Name = "John Doe",
                Email = "john.doe@test.com",
                Password = "SecretPassword123",
                DepartmentId = 1
            };
            var result = await _authService.Register(request);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Email, Is.EqualTo(request.Email));

            var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            Assert.That(dbUser, Is.Not.Null);
            Assert.That(dbUser.Name, Is.EqualTo(request.Name));

            Assert.That(dbUser.PasswordHash, Is.Not.EqualTo(request.Password));

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(al => al.PerformedByUserId == dbUser.Id);
            Assert.That(auditLog, Is.Not.Null);
            Assert.That(auditLog.Action.ToString(), Is.EqualTo("UserRegistered"));
        }

        [Test]
        public async Task Login_ValidCredentials_ReturnsResponseWithTokenAndCreatesAuditLog()
        {
            var registerRequest = new RegisterRequestDto
            {
                Name = "Login Test User",
                Email = "login.test@test.com",
                Password = "Password123!",
                DepartmentId = 1
            };
            await _authService.Register(registerRequest);

            var loginRequest = new LoginRequestDto
            {
                Email = "login.test@test.com",
                Password = "Password123!"
            };

            var result = await _authService.Login(loginRequest);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.UserId, Is.GreaterThan(0));
            Assert.That(result.Name, Is.EqualTo(registerRequest.Name));
            Assert.That(result.Email, Is.EqualTo(registerRequest.Email));
            Assert.That(result.Role, Is.EqualTo("User"));
            Assert.That(result.Token, Is.Not.Null.And.Not.Empty);

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(al => 
                al.PerformedByUserId == result.UserId && al.Details != null && al.Details.Contains("User logged in"));
            Assert.That(auditLog, Is.Not.Null);
            Assert.That(auditLog.Action.ToString(), Is.EqualTo("UserRegistered"));
        }

        [Test]
        public async Task Login_InvalidPassword_ThrowsUnauthorizedAccessException()
        {
            var registerRequest = new RegisterRequestDto
            {
                Name = "Login Test User",
                Email = "login.wrongpass@test.com",
                Password = "Password123!",
                DepartmentId = 1
            };
            await _authService.Register(registerRequest);

            var loginRequest = new LoginRequestDto
            {
                Email = "login.wrongpass@test.com",
                Password = "WrongPassword!"
            };

            Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await _authService.Login(loginRequest));
        }

        [Test]
        public async Task Login_NonExistentEmail_ThrowsUnauthorizedAccessException()
        {
            var loginRequest = new LoginRequestDto
            {
                Email = "doesnotexist@test.com",
                Password = "Password123!"
            };

            Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await _authService.Login(loginRequest));
        }
    }
}