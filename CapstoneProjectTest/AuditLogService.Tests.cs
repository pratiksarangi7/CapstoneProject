using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Services;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace CapstoneProjectTest
{
    [TestFixture]
    public class AuditLogServiceTests
    {
        private AuditLogService _auditLogService;
        private AppDbContext _context;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "CapstoneAuditDb_" + Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _auditLogService = new AuditLogService(_context);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task GetAuditLogs_WithoutActionFilter_ReturnsAllAuditLogs()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "John Doe", Email = "john@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _context.AuditLogs.AddRange(
                new AuditLog { Action = AuditAction.UserRegistered, PerformedByUserId = user.Id, Details = "Reg", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10) },
                new AuditLog { Action = AuditAction.UserLoggedIn, PerformedByUserId = user.Id, Details = "Login", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5) }
            );
            await _context.SaveChangesAsync();

            var result = await _auditLogService.GetAuditLogs(action: null, pageNumber: 1, pageSize: 10);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.Items.Count, Is.EqualTo(2));
            Assert.That(result.Items.First().Action, Is.EqualTo("UserLoggedIn"));
            Assert.That(result.Items.Last().Action, Is.EqualTo("UserRegistered"));
        }

        [Test]
        public async Task GetAuditLogs_WithActionFilter_ReturnsOnlyMatchingAuditLogs()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "John Doe", Email = "john@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _context.AuditLogs.AddRange(
                new AuditLog { Action = AuditAction.UserRegistered, PerformedByUserId = user.Id, Details = "Reg", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10) },
                new AuditLog { Action = AuditAction.UserLoggedIn, PerformedByUserId = user.Id, Details = "Login", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5) }
            );
            await _context.SaveChangesAsync();

            var result = await _auditLogService.GetAuditLogs(action: AuditAction.UserLoggedIn, pageNumber: 1, pageSize: 10);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(result.Items.First().Action, Is.EqualTo("UserLoggedIn"));
            Assert.That(result.Items.First().Details, Is.EqualTo("Login"));
        }

        [Test]
        public async Task GetAuditLogs_PaginationCoercesInvalidParameters()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "John Doe", Email = "john@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            for (int i = 0; i < 15; i++)
            {
                _context.AuditLogs.Add(new AuditLog { Action = AuditAction.UserRegistered, PerformedByUserId = user.Id, Details = $"Reg {i}" });
            }
            await _context.SaveChangesAsync();

            var result = await _auditLogService.GetAuditLogs(action: null, pageNumber: -5, pageSize: 150);

            Assert.That(result.PageNumber, Is.EqualTo(1));
            Assert.That(result.PageSize, Is.EqualTo(10));
            Assert.That(result.Items.Count, Is.EqualTo(10));
        }

        [Test]
        public async Task GetAuditLogs_WithRelatedEntities_CorrectlyMapsDetails()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "John Doe", Email = "john@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var document = new Document { Title = "Important Doc", CreatedByUserId = user.Id, TargetDepartmentId = dept.Id };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var version = new DocumentVersion { OriginalFileName = "doc.pdf", StoredFileName = "stored.pdf", MimeType = "application/pdf", DocumentId = document.Id, UploadedByUserId = user.Id, VersionNumber = 2 };
            _context.DocumentVersions.Add(version);
            await _context.SaveChangesAsync();

            var auditLog = new AuditLog
            {
                Action = AuditAction.DocumentUploaded,
                PerformedByUserId = user.Id,
                DocumentId = document.Id,
                DocumentVersionId = version.Id,
                Details = "Uploaded document version 2"
            };
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            var result = await _auditLogService.GetAuditLogs(action: null, pageNumber: 1, pageSize: 10);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Items.Count, Is.EqualTo(1));

            var item = result.Items.First();
            Assert.That(item.Id, Is.EqualTo(auditLog.Id));
            Assert.That(item.PerformedByUserId, Is.EqualTo(user.Id));
            Assert.That(item.PerformedByUserName, Is.EqualTo("John Doe"));
            Assert.That(item.PerformedByUserEmail, Is.EqualTo("john@test.com"));
            Assert.That(item.DocumentId, Is.EqualTo(document.Id));
            Assert.That(item.DocumentTitle, Is.EqualTo("Important Doc"));
            Assert.That(item.DocumentVersionId, Is.EqualTo(version.Id));
            Assert.That(item.DocumentVersionNumber, Is.EqualTo(2));
            Assert.That(item.Action, Is.EqualTo("DocumentUploaded"));
            Assert.That(item.Details, Is.EqualTo("Uploaded document version 2"));
        }

        [Test]
        public async Task GetAuditLogs_WithUserIdFilter_ReturnsOnlyMatchingAuditLogs()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user1 = new User { Name = "User One", Email = "user1@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var user2 = new User { Name = "User Two", Email = "user2@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(user1, user2);
            await _context.SaveChangesAsync();

            _context.AuditLogs.AddRange(
                new AuditLog { Action = AuditAction.UserRegistered, PerformedByUserId = user1.Id, Details = "User1 Reg", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10) },
                new AuditLog { Action = AuditAction.UserRegistered, PerformedByUserId = user2.Id, Details = "User2 Reg", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5) }
            );
            await _context.SaveChangesAsync();

            var result = await _auditLogService.GetAuditLogs(action: null, userId: user1.Id, pageNumber: 1, pageSize: 10);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(result.Items.First().PerformedByUserId, Is.EqualTo(user1.Id));
            Assert.That(result.Items.First().Details, Is.EqualTo("User1 Reg"));
        }
    }
}