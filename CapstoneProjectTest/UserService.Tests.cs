using Microsoft.EntityFrameworkCore;
using AutoMapper;
using System;
using System.Threading.Tasks;
using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Services;
using CapstoneProjectAPI.Mappings;
using CapstoneProjectAPI.Exceptions;
using CapstoneProjectAPI.Models.Enums;

namespace CapstoneProjectTest
{
    [TestFixture]
    public class UserServiceTests
    {
        private AppDbContext _context;
        private IMapper _mapper;
        private UserService _userService;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "CapstoneUserDb_" + Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);

            var mappingConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

            _mapper = mappingConfig.CreateMapper();
            _userService = new UserService(_context, _mapper);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task GetUserDetailsAsync_UserExists_ReturnsMappedDetails()
        {
            var dept = new Department { Name = "Engineering" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var manager = new User { Name = "Jane Manager", Email = "jane@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(manager);
            await _context.SaveChangesAsync();

            var user = new User { Name = "John Employee", Email = "john@test.com", PasswordHash = "hash", DepartmentId = dept.Id, ManagerId = manager.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var result = await _userService.GetUserDetailsAsync(user.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo("John Employee"));
            Assert.That(result.Email, Is.EqualTo("john@test.com"));
            Assert.That(result.DepartmentName, Is.EqualTo("Engineering"));
            Assert.That(result.ManagerName, Is.EqualTo("Jane Manager"));
        }

        [Test]
        public void GetUserDetailsAsync_UserNotFound_ThrowsEntityNotFoundException()
        {
            Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _userService.GetUserDetailsAsync(999));
        }

        [Test]
        public async Task GetUserDetailsAsync_UserWithoutManager_ReturnsMappedDetailsWithNullManager()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Alice Single", Email = "alice@test.com", PasswordHash = "hash", DepartmentId = dept.Id, ManagerId = null };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var result = await _userService.GetUserDetailsAsync(user.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo("Alice Single"));
            Assert.That(result.Email, Is.EqualTo("alice@test.com"));
            Assert.That(result.DepartmentName, Is.EqualTo("HR"));
            Assert.That(result.ManagerId, Is.Null);
            Assert.That(result.ManagerName, Is.Null);
        }

        [Test]
        public async Task GetUserDetailsAsync_UserIsAdmin_ReturnsMappedDetailsWithIsAdminTrue()
        {
            var dept = new Department { Name = "IT Support" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Bob Admin", Email = "bob@test.com", PasswordHash = "hash", DepartmentId = dept.Id, IsAdmin = true };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var result = await _userService.GetUserDetailsAsync(user.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo("Bob Admin"));
            Assert.That(result.IsAdmin, Is.True);
        }

        [Test]
        public void ChangePasswordAsync_UserDoesNotExist_ThrowsEntityNotFoundException()
        {
            var request = new ChangePasswordRequestDto
            {
                OldPassword = "OldPassword123!",
                NewPassword = "NewPassword123!"
            };

            Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _userService.ChangePasswordAsync(999, request));
        }

        [Test]
        public async Task ChangePasswordAsync_IncorrectOldPassword_ThrowsUnauthorizedAccessException()
        {
            var dept = new Department { Name = "IT Support" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User
            {
                Name = "John Doe",
                Email = "john.doe@test.com",
                PasswordHash = HashPasswordHelper("CorrectOldPassword123!"),
                DepartmentId = dept.Id
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ChangePasswordRequestDto
            {
                OldPassword = "WrongOldPassword123!",
                NewPassword = "NewPassword123!"
            };

            var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _userService.ChangePasswordAsync(user.Id, request));
            Assert.That(ex.Message, Is.EqualTo("Old password is incorrect."));
        }

        [Test]
        public async Task ChangePasswordAsync_NewPasswordSameAsOld_ThrowsArgumentException()
        {
            var dept = new Department { Name = "IT Support" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User
            {
                Name = "John Doe",
                Email = "john.doe@test.com",
                PasswordHash = HashPasswordHelper("SamePassword123!"),
                DepartmentId = dept.Id
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ChangePasswordRequestDto
            {
                OldPassword = "SamePassword123!",
                NewPassword = "SamePassword123!"
            };

            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _userService.ChangePasswordAsync(user.Id, request));
            Assert.That(ex.Message, Is.EqualTo("New password must be different from the old password."));
        }

        [Test]
        public async Task ChangePasswordAsync_ValidRequest_ChangesPasswordAndCreatesAuditLog()
        {
            var dept = new Department { Name = "IT Support" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User
            {
                Name = "John Doe",
                Email = "john.doe@test.com",
                PasswordHash = HashPasswordHelper("OldPassword123!"),
                DepartmentId = dept.Id
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var oldPasswordHash = user.PasswordHash;

            var request = new ChangePasswordRequestDto
            {
                OldPassword = "OldPassword123!",
                NewPassword = "NewPassword123!"
            };

            await _userService.ChangePasswordAsync(user.Id, request);

            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.That(updatedUser, Is.Not.Null);
            Assert.That(updatedUser.PasswordHash, Is.Not.EqualTo(oldPasswordHash));

            var secondRequest = new ChangePasswordRequestDto
            {
                OldPassword = "NewPassword123!",
                NewPassword = "AnotherNewPassword123!"
            };
            Assert.DoesNotThrowAsync(async () => await _userService.ChangePasswordAsync(user.Id, secondRequest));

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(al => al.PerformedByUserId == user.Id);
            Assert.That(auditLog, Is.Not.Null);
            Assert.That(auditLog.Action, Is.EqualTo(AuditAction.PasswordChanged));
            Assert.That(auditLog.Details, Is.EqualTo($"User '{user.Name}' changed their password."));
        }

        private static string HashPasswordHelper(string password)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256();
            byte[] hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            string keyB64 = Convert.ToBase64String(hmac.Key);
            string hashB64 = Convert.ToBase64String(hash);
            return $"{keyB64}:{hashB64}";
        }
    }
}
