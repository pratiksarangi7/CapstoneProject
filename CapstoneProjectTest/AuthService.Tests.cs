using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using AutoMapper;
using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Services;
using CapstoneProjectAPI.Mappings;
using Microsoft.Extensions.Logging;
using Moq;

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
                .UseInMemoryDatabase(databaseName: "CapstoneAuthDb_" + Guid.NewGuid().ToString())
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
            var mockLogger = new Mock<ILogger<AuthenticationService>>();
            _authService = new AuthenticationService(_context, _configuration, _mapper, mockLogger.Object);
        }
        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
        #region Register
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
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Register_EmptyEmail_ThrowsArgumentException(string? email)
        {
            var request = new RegisterRequestDto
            {
                Name = "John Doe",
                Email = email!,
                Password = "Password123!",
                DepartmentId = 1
            };

            var ex = Assert.ThrowsAsync<ArgumentException>(async () => await _authService.Register(request));
            Assert.That(ex.Message, Is.EqualTo("Email is required."));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Register_EmptyPassword_ThrowsArgumentException(string? password)
        {
            var request = new RegisterRequestDto
            {
                Name = "John Doe",
                Email = "john.doe@test.com",
                Password = password!,
                DepartmentId = 1
            };

            var ex = Assert.ThrowsAsync<ArgumentException>(async () => await _authService.Register(request));
            Assert.That(ex.Message, Is.EqualTo("Password is required."));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Register_EmptyName_ThrowsArgumentException(string? name)
        {
            var request = new RegisterRequestDto
            {
                Name = name!,
                Email = "john.doe@test.com",
                Password = "Password123!",
                DepartmentId = 1
            };

            var ex = Assert.ThrowsAsync<ArgumentException>(async () => await _authService.Register(request));
            Assert.That(ex.Message, Is.EqualTo("Name is required."));
        }

        [Test]
        [TestCase(0)]
        [TestCase(-1)]
        public void Register_InvalidDepartmentId_ThrowsArgumentException(int departmentId)
        {
            var request = new RegisterRequestDto
            {
                Name = "John Doe",
                Email = "john.doe@test.com",
                Password = "Password123!",
                DepartmentId = departmentId
            };

            var ex = Assert.ThrowsAsync<ArgumentException>(async () => await _authService.Register(request));
            Assert.That(ex.Message, Is.EqualTo("A valid Department ID is required."));
        }

        [Test]
        public async Task Register_EmailAlreadyExists_ThrowsInvalidOperationException()
        {
            var request1 = new RegisterRequestDto
            {
                Name = "John Doe",
                Email = "duplicate@test.com",
                Password = "Password123!",
                DepartmentId = 1
            };
            await _authService.Register(request1);

            var request2 = new RegisterRequestDto
            {
                Name = "Jane Doe",
                Email = "duplicate@test.com",
                Password = "AnotherPassword123!",
                DepartmentId = 1
            };

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await _authService.Register(request2));
            Assert.That(ex.Message, Is.EqualTo("A user with email 'duplicate@test.com' already exists."));
        }
        #endregion
        #region Login
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

            var registeredUser = await _context.Users.FirstAsync(u => u.Email == registerRequest.Email);
            registeredUser.IsActive = true;
            await _context.SaveChangesAsync();

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
            Assert.That(auditLog.Action.ToString(), Is.EqualTo("UserLoggedIn"));
        }
        #endregion
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

        [Test]
        public async Task Login_DeactivatedUser_ThrowsUnauthorizedAccessException()
        {
            var registerRequest = new RegisterRequestDto
            {
                Name = "Deactivated User",
                Email = "deactivated@test.com",
                Password = "Password123!",
                DepartmentId = 1
            };
            await _authService.Register(registerRequest);

            var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "deactivated@test.com");
            Assert.That(dbUser, Is.Not.Null);
            dbUser!.IsActive = false;
            await _context.SaveChangesAsync();

            var loginRequest = new LoginRequestDto
            {
                Email = "deactivated@test.com",
                Password = "Password123!"
            };

            var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await _authService.Login(loginRequest));
            Assert.That(ex.Message, Is.EqualTo("Your account has been deactivated. Please contact your administrator."));
        }

        [Test]
        public async Task Login_VerifyPasswordThrowsException_ThrowsUnauthorizedAccessException()
        {
            var registerRequest = new RegisterRequestDto
            {
                Name = "Corrupted Pass User",
                Email = "corrupted.pass@test.com",
                Password = "Password123!",
                DepartmentId = 1
            };
            await _authService.Register(registerRequest);

            var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "corrupted.pass@test.com");
            Assert.That(dbUser, Is.Not.Null);
            dbUser!.PasswordHash = "invalid-base64:invalid-base64@@@";
            await _context.SaveChangesAsync();

            var loginRequest = new LoginRequestDto
            {
                Email = "corrupted.pass@test.com",
                Password = "Password123!"
            };

            var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await _authService.Login(loginRequest));
            Assert.That(ex.Message, Is.EqualTo("Invalid email or password."));
        }
    }
}