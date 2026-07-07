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

            // Test pageSize < 0 (coerces to 10)
            var result2 = await _auditLogService.GetAuditLogs(action: null, pageNumber: 1, pageSize: -5);
            Assert.That(result2.PageSize, Is.EqualTo(10));
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

        [Test]
        public async Task GetAuditLogsByActions_ParameterCoercion_CoercesSuccessfully()
        {
            var resultPageNumber = await _auditLogService.GetAuditLogsByActions(actions: null!, userId: null, pageNumber: -5, pageSize: 10);
            Assert.That(resultPageNumber.PageNumber, Is.EqualTo(1));

            var resultPageSizeNegative = await _auditLogService.GetAuditLogsByActions(actions: null!, userId: null, pageNumber: 1, pageSize: -1);
            Assert.That(resultPageSizeNegative.PageSize, Is.EqualTo(10));

            var resultPageSizeTooLarge = await _auditLogService.GetAuditLogsByActions(actions: null!, userId: null, pageNumber: 1, pageSize: 150);
            Assert.That(resultPageSizeTooLarge.PageSize, Is.EqualTo(10));
        }

        [Test]
        public async Task GetAuditLogsByActions_NullOrEmptyActions_ReturnsAllLogs()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "User One", Email = "user1@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _context.AuditLogs.AddRange(
                new AuditLog { Action = AuditAction.UserRegistered, PerformedByUserId = user.Id, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10) },
                new AuditLog { Action = AuditAction.UserLoggedIn, PerformedByUserId = user.Id, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5) }
            );
            await _context.SaveChangesAsync();

            // Null actions list
            var resultNull = await _auditLogService.GetAuditLogsByActions(actions: null!, userId: null, pageNumber: 1, pageSize: 10);
            Assert.That(resultNull.TotalCount, Is.EqualTo(2));

            // Empty actions list
            var resultEmpty = await _auditLogService.GetAuditLogsByActions(actions: new List<AuditAction>(), userId: null, pageNumber: 1, pageSize: 10);
            Assert.That(resultEmpty.TotalCount, Is.EqualTo(2));
        }

        [Test]
        public async Task GetAuditLogsByActions_WithActionsFilter_ReturnsOnlyMatchingActions()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "User One", Email = "user1@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _context.AuditLogs.AddRange(
                new AuditLog { Action = AuditAction.UserRegistered, PerformedByUserId = user.Id, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-15) },
                new AuditLog { Action = AuditAction.UserLoggedIn, PerformedByUserId = user.Id, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10) },
                new AuditLog { Action = AuditAction.DocumentUploaded, PerformedByUserId = user.Id, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5) }
            );
            await _context.SaveChangesAsync();

            var filter = new List<AuditAction> { AuditAction.UserRegistered, AuditAction.DocumentUploaded };
            var result = await _auditLogService.GetAuditLogsByActions(actions: filter, userId: null, pageNumber: 1, pageSize: 10);

            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.Items.Any(item => item.Action == "UserRegistered"), Is.True);
            Assert.That(result.Items.Any(item => item.Action == "DocumentUploaded"), Is.True);
            Assert.That(result.Items.Any(item => item.Action == "UserLoggedIn"), Is.False);
        }

        [Test]
        public async Task GetAuditLogsByActions_WithUserIdFilter_ReturnsOnlyMatchingUserLogs()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user1 = new User { Name = "User One", Email = "user1@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var user2 = new User { Name = "User Two", Email = "user2@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(user1, user2);
            await _context.SaveChangesAsync();

            _context.AuditLogs.AddRange(
                new AuditLog { Action = AuditAction.UserRegistered, PerformedByUserId = user1.Id, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10) },
                new AuditLog { Action = AuditAction.UserRegistered, PerformedByUserId = user2.Id, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5) }
            );
            await _context.SaveChangesAsync();

            var result = await _auditLogService.GetAuditLogsByActions(actions: null!, userId: user1.Id, pageNumber: 1, pageSize: 10);

            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items.First().PerformedByUserId, Is.EqualTo(user1.Id));
        }

        [Test]
        public async Task GetAuditLogsByActions_NullNavigationProperties_ReturnsEmptyStringsOrNulls()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Alice", Email = "alice@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Set up a log with a valid user but null document and version references
            var log = new AuditLog
            {
                Action = AuditAction.UserRegistered,
                PerformedByUserId = user.Id,
                DocumentId = null,
                DocumentVersionId = null,
                Details = "Direct log",
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();

            var result = await _auditLogService.GetAuditLogsByActions(actions: null!, userId: null, pageNumber: 1, pageSize: 10);
            
            Assert.That(result.TotalCount, Is.EqualTo(1));
            var item = result.Items.First();
            Assert.That(item.PerformedByUserName, Is.EqualTo("Alice"));
            Assert.That(item.PerformedByUserEmail, Is.EqualTo("alice@test.com"));
            Assert.That(item.DocumentTitle, Is.Null);
            Assert.That(item.DocumentVersionNumber, Is.Null);
        }

        [Test]
        public async Task GetAuditLogsByActions_LoadedNavigationProperties_ReturnsCorrectDetails()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Alice", Email = "alice@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var doc = new Document { Title = "Contract", CreatedByUserId = user.Id, TargetDepartmentId = dept.Id };
            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();

            var version = new DocumentVersion { DocumentId = doc.Id, VersionNumber = 3, IsCurrentVersion = true, UploadedByUserId = user.Id };
            _context.DocumentVersions.Add(version);
            await _context.SaveChangesAsync();

            var log = new AuditLog
            {
                Action = AuditAction.DocumentUploaded,
                PerformedByUserId = user.Id,
                DocumentId = doc.Id,
                DocumentVersionId = version.Id,
                Details = "Uploaded v3",
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();

            var result = await _auditLogService.GetAuditLogsByActions(actions: null!, userId: null, pageNumber: 1, pageSize: 10);
            
            Assert.That(result.TotalCount, Is.EqualTo(1));
            var item = result.Items.First();
            Assert.That(item.PerformedByUserName, Is.EqualTo("Alice"));
            Assert.That(item.PerformedByUserEmail, Is.EqualTo("alice@test.com"));
            Assert.That(item.DocumentTitle, Is.EqualTo("Contract"));
            Assert.That(item.DocumentVersionId, Is.EqualTo(version.Id));
            Assert.That(item.DocumentVersionNumber, Is.EqualTo(3));
        }
    }
}