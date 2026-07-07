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
using Microsoft.Extensions.Logging;
using Moq;

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
            var mockLogger = new Mock<ILogger<UserService>>();
            _userService = new UserService(_context, _mapper, mockLogger.Object, new AuditLogService(_context));
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

        [Test]
        public async Task ChangePasswordAsync_VerifyPasswordThrowsException_ThrowsUnauthorizedAccessException()
        {
            var dept = new Department { Name = "IT Support" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User
            {
                Name = "John Doe",
                Email = "john.doe@test.com",
                PasswordHash = "invalid-base64:invalid-base64@@@",
                DepartmentId = dept.Id
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ChangePasswordRequestDto
            {
                OldPassword = "AnyPassword123!",
                NewPassword = "NewPassword123!"
            };

            var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _userService.ChangePasswordAsync(user.Id, request));
            Assert.That(ex.Message, Is.EqualTo("Old password is incorrect."));
        }

        private static string HashPasswordHelper(string password)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256();
            byte[] hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            string keyB64 = Convert.ToBase64String(hmac.Key);
            string hashB64 = Convert.ToBase64String(hash);
            return $"{keyB64}:{hashB64}";
        }

        [Test]
        public async Task GetDepartmentsAsync_ReturnsDepartments_OrderedByName()
        {
            _context.Departments.AddRange(
                new Department { Name = "IT" },
                new Department { Name = "HR" },
                new Department { Name = "Finance" }
            );
            await _context.SaveChangesAsync();

            var result = await _userService.GetDepartmentsAsync();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result[0].Name, Is.EqualTo("Finance"));
            Assert.That(result[1].Name, Is.EqualTo("HR"));
            Assert.That(result[2].Name, Is.EqualTo("IT"));
        }

        [Test]
        public void GetUsersOutsideDepartmentAsync_UserNotFound_ThrowsEntityNotFoundException()
        {
            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _userService.GetUsersOutsideDepartmentAsync(9999));
            Assert.That(ex.Message, Is.EqualTo("User with ID 9999 was not found."));
        }

        [Test]
        public async Task GetUsersOutsideDepartmentAsync_ReturnsUsersInOtherDepartments_OrderedByName()
        {
            var dept1 = new Department { Name = "HR" };
            var dept2 = new Department { Name = "IT" };
            _context.Departments.AddRange(dept1, dept2);
            await _context.SaveChangesAsync();

            var currentUser = new User { Name = "Current", Email = "current@test.com", PasswordHash = "hash", DepartmentId = dept1.Id };
            var sameDeptUser = new User { Name = "Same Dept", Email = "same@test.com", PasswordHash = "hash", DepartmentId = dept1.Id };
            var otherUser1 = new User { Name = "Zack", Email = "zack@test.com", PasswordHash = "hash", DepartmentId = dept2.Id };
            var otherUser2 = new User { Name = "Abby", Email = "abby@test.com", PasswordHash = "hash", DepartmentId = dept2.Id };

            _context.Users.AddRange(currentUser, sameDeptUser, otherUser1, otherUser2);
            await _context.SaveChangesAsync();

            var result = await _userService.GetUsersOutsideDepartmentAsync(currentUser.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(2));
            
            // Ordered by name alphabetically (Abby then Zack)
            Assert.That(result[0].Id, Is.EqualTo(otherUser2.Id));
            Assert.That(result[0].Name, Is.EqualTo("Abby"));
            Assert.That(result[0].Email, Is.EqualTo("abby@test.com"));
            Assert.That(result[0].DepartmentName, Is.EqualTo("IT"));

            Assert.That(result[1].Id, Is.EqualTo(otherUser1.Id));
            Assert.That(result[1].Name, Is.EqualTo("Zack"));
            Assert.That(result[1].Email, Is.EqualTo("zack@test.com"));
            Assert.That(result[1].DepartmentName, Is.EqualTo("IT"));
        }

        [Test]
        public async Task GetUserDocumentActionsAsync_ReturnsOnlyMatchingActionsForUser()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user1 = new User { Name = "User One", Email = "user1@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var user2 = new User { Name = "User Two", Email = "user2@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(user1, user2);
            await _context.SaveChangesAsync();

            _context.AuditLogs.AddRange(
                // Matching actions for user1
                new AuditLog { Action = AuditAction.DocumentApproved, PerformedByUserId = user1.Id, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20) },
                new AuditLog { Action = AuditAction.DocumentRejected, PerformedByUserId = user1.Id, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-15) },
                new AuditLog { Action = AuditAction.DocumentForwarded, PerformedByUserId = user1.Id, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10) },
                
                // Non-matching action for user1
                new AuditLog { Action = AuditAction.UserRegistered, PerformedByUserId = user1.Id, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5) },
                
                // Matching action but for user2
                new AuditLog { Action = AuditAction.DocumentApproved, PerformedByUserId = user2.Id, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1) }
            );
            await _context.SaveChangesAsync();

            var result = await _userService.GetUserDocumentActionsAsync(user1.Id, pageNumber: 1, pageSize: 10);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.TotalCount, Is.EqualTo(3));
            Assert.That(result.Items.Count, Is.EqualTo(3));

            // Verify they are returned in descending order of CreatedAt
            Assert.That(result.Items[0].Action, Is.EqualTo("DocumentForwarded"));
            Assert.That(result.Items[1].Action, Is.EqualTo("DocumentRejected"));
            Assert.That(result.Items[2].Action, Is.EqualTo("DocumentApproved"));

            foreach (var item in result.Items)
            {
                Assert.That(item.PerformedByUserId, Is.EqualTo(user1.Id));
            }
        }
    }
}
