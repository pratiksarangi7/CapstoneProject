using Microsoft.EntityFrameworkCore;
using AutoMapper;
using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Services;
using CapstoneProjectAPI.Mappings;
using CapstoneProjectAPI.Exceptions;
using CapstoneProjectAPI.Models.Enums;
using Moq;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;

namespace CapstoneProjectTest
{
    [TestFixture]
    public class AdminServiceTests
    {
        private AppDbContext _context;
        private IMapper _mapper;
        private AdminService _adminService;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "CapstoneAdminDb_" + Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);

            var mappingConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

            _mapper = mappingConfig.CreateMapper();
            _adminService = new AdminService(_context, _mapper);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task GetUsers_ReturnsPagedUsers_Successfully()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            for (int i = 1; i <= 15; i++)
            {
                _context.Users.Add(new User
                {
                    Name = $"User {i}",
                    Email = $"user{i}@test.com",
                    PasswordHash = "hashed",
                    DepartmentId = dept.Id
                });
            }
            await _context.SaveChangesAsync();

            var result = await _adminService.GetUsers(pageNumber: 2, pageSize: 5);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Items.Count, Is.EqualTo(5));
            Assert.That(result.TotalCount, Is.EqualTo(15));
            Assert.That(result.PageNumber, Is.EqualTo(2));
            Assert.That(result.PageSize, Is.EqualTo(5));
            Assert.That(result.TotalPages, Is.EqualTo(3));
            Assert.That(result.Items.First().Name, Is.EqualTo("User 6"));
        }

        [Test]
        public async Task GetUsers_CoercesInvalidParameters()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            _context.Users.Add(new User { Name = "User 1", Email = "user1@test.com", PasswordHash = "hashed", DepartmentId = dept.Id });
            await _context.SaveChangesAsync();

            var result = await _adminService.GetUsers(pageNumber: -1, pageSize: 200);

            Assert.That(result.PageNumber, Is.EqualTo(1));
            Assert.That(result.PageSize, Is.EqualTo(10));
        }

        [Test]
        public async Task ChangeUserManager_ValidRequest_UpdatesManagerSuccessfully()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            var manager = new User { Name = "Manager", Email = "mgr@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            _context.Users.AddRange(user, manager);
            await _context.SaveChangesAsync();

            var request = new ChangeUserManagerRequestDto
            {
                UserId = user.Id,
                ManagerId = manager.Id
            };

            var result = await _adminService.ChangeUserManager(request);
            Assert.That(result, Is.True);
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.That(updatedUser, Is.Not.Null);
            Assert.That(updatedUser!.ManagerId, Is.EqualTo(manager.Id));
        }

        [Test]
        public async Task ChangeUserManager_NullManagerId_ClearsManager()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var manager = new User { Name = "Manager", Email = "mgr@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            _context.Users.Add(manager);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = dept.Id, ManagerId = manager.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ChangeUserManagerRequestDto
            {
                UserId = user.Id,
                ManagerId = null
            };

            var result = await _adminService.ChangeUserManager(request);

            Assert.That(result, Is.True);
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.That(updatedUser, Is.Not.Null);
            Assert.That(updatedUser!.ManagerId, Is.Null);
        }

        [Test]
        public void ChangeUserManager_UserNotFound_ThrowsEntityNotFoundException()
        {
            var request = new ChangeUserManagerRequestDto
            {
                UserId = 999,
                ManagerId = 1
            };

            Assert.ThrowsAsync<EntityNotFoundException>(async () => await _adminService.ChangeUserManager(request));
        }

        [Test]
        public async Task ChangeUserManager_ManagerNotFound_ThrowsEntityNotFoundException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ChangeUserManagerRequestDto
            {
                UserId = user.Id,
                ManagerId = 999
            };
            Assert.ThrowsAsync<EntityNotFoundException>(async () => await _adminService.ChangeUserManager(request));
        }

        [Test]
        public async Task ChangeUserManager_SelfAsManager_ThrowsArgumentException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ChangeUserManagerRequestDto
            {
                UserId = user.Id,
                ManagerId = user.Id
            };

            Assert.ThrowsAsync<ArgumentException>(async () => await _adminService.ChangeUserManager(request));
        }

        [Test]
        public async Task ChangeUserLevel_ValidRequest_UpdatesLevelSuccessfully()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = dept.Id, Level = 1 };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ChangeUserLevelRequestDto
            {
                UserId = user.Id,
                Level = 5
            };

            var result = await _adminService.ChangeUserLevel(request);

            Assert.That(result, Is.True);
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.That(updatedUser, Is.Not.Null);
            Assert.That(updatedUser!.Level, Is.EqualTo(5));
        }

        [Test]
        public void ChangeUserLevel_LevelLessThanOne_ThrowsArgumentException()
        {
            var request = new ChangeUserLevelRequestDto
            {
                UserId = 1,
                Level = 0
            };

            Assert.ThrowsAsync<ArgumentException>(async () => await _adminService.ChangeUserLevel(request));
        }

        [Test]
        public void ChangeUserLevel_UserNotFound_ThrowsEntityNotFoundException()
        {
            var request = new ChangeUserLevelRequestDto
            {
                UserId = 999,
                Level = 2
            };

            Assert.ThrowsAsync<EntityNotFoundException>(async () => await _adminService.ChangeUserLevel(request));
        }

        [Test]
        public async Task ChangeUserDepartment_ValidRequest_UpdatesDepartmentSuccessfully()
        {
            var deptA = new Department { Name = "HR" };
            var deptB = new Department { Name = "Engineering" };
            _context.Departments.AddRange(deptA, deptB);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = deptA.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ChangeUserDepartmentRequestDto
            {
                UserId = user.Id,
                DepartmentId = deptB.Id
            };

            var result = await _adminService.ChangeUserDepartment(request);
            Assert.That(result, Is.True);
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.That(updatedUser, Is.Not.Null);
            Assert.That(updatedUser!.DepartmentId, Is.EqualTo(deptB.Id));
        }

        [Test]
        public void ChangeUserDepartment_UserNotFound_ThrowsEntityNotFoundException()
        {
            var request = new ChangeUserDepartmentRequestDto
            {
                UserId = 999,
                DepartmentId = 1
            };
            Assert.ThrowsAsync<EntityNotFoundException>(async () => await _adminService.ChangeUserDepartment(request));
        }

        [Test]
        public async Task ChangeUserDepartment_DepartmentNotFound_ThrowsEntityNotFoundException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ChangeUserDepartmentRequestDto
            {
                UserId = user.Id,
                DepartmentId = 999
            };
            Assert.ThrowsAsync<EntityNotFoundException>(async () => await _adminService.ChangeUserDepartment(request));
        }

        [Test]
        public async Task AddDepartment_ValidRequest_AddsAndReturnsDepartment()
        {
            var request = new AddDepartmentRequestDto
            {
                Name = "Finance"
            };
            var result = await _adminService.AddDepartment(request);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.GreaterThan(0));
            Assert.That(result.Name, Is.EqualTo("Finance"));

            var inDb = await _context.Departments.AnyAsync(d => d.Name == "Finance");
            Assert.That(inDb, Is.True);
        }

        [Test]
        public void AddDepartment_EmptyName_ThrowsArgumentException()
        {
            var request = new AddDepartmentRequestDto { Name = "   " };
            Assert.ThrowsAsync<ArgumentException>(async () => await _adminService.AddDepartment(request));
        }

        [Test]
        public async Task AddDepartment_DuplicateName_ThrowsInvalidOperationException()
        {
            _context.Departments.Add(new Department { Name = "Marketing" });
            await _context.SaveChangesAsync();
            var request = new AddDepartmentRequestDto { Name = "marketing" };
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _adminService.AddDepartment(request));
        }

        [Test]
        public async Task GetDepartments_ReturnsDepartmentsWithUsersAndManagers()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();
            var manager = new User { Name = "Manager", Email = "mgr@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            _context.Users.Add(manager);
            await _context.SaveChangesAsync();
            var employee = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = dept.Id, ManagerId = manager.Id };
            _context.Users.Add(employee);
            await _context.SaveChangesAsync();
            var result = await _adminService.GetDepartments();
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.GreaterThanOrEqualTo(1));
            var hrDept = result.FirstOrDefault(d => d.Name == "HR");
            Assert.That(hrDept, Is.Not.Null);
            Assert.That(hrDept!.Users.Count, Is.EqualTo(2));
            var empDto = hrDept.Users.FirstOrDefault(u => u.Name == "Employee");
            Assert.That(empDto, Is.Not.Null);
            Assert.That(empDto!.ManagerName, Is.EqualTo("Manager"));
        }

        [Test]
        public void ReassignDocuments_FromApproverNotFound_ThrowsEntityNotFoundException()
        {
            var request = new ReassignDocumentsRequestDto
            {
                FromApproverId = 999,
                ToApproverId = 1
            };

            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _adminService.ReassignDocuments(request, 100));
            Assert.That(ex.Message, Is.EqualTo("Source approver user not found"));
        }

        [Test]
        public async Task ReassignDocuments_ToApproverNotFound_ThrowsEntityNotFoundException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var fromUser = new User { Name = "From", Email = "from@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            _context.Users.Add(fromUser);
            await _context.SaveChangesAsync();

            var request = new ReassignDocumentsRequestDto
            {
                FromApproverId = fromUser.Id,
                ToApproverId = 999
            };

            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _adminService.ReassignDocuments(request, 100));
            Assert.That(ex.Message, Is.EqualTo("Target approver user not found"));
        }

        [Test]
        public async Task ReassignDocuments_DocumentNotFound_ThrowsEntityNotFoundException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var fromUser = new User { Name = "From", Email = "from@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            var toUser = new User { Name = "To", Email = "to@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            _context.Users.AddRange(fromUser, toUser);
            await _context.SaveChangesAsync();

            var request = new ReassignDocumentsRequestDto
            {
                FromApproverId = fromUser.Id,
                ToApproverId = toUser.Id,
                DocumentId = 999
            };

            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _adminService.ReassignDocuments(request, 100));
            Assert.That(ex.Message, Is.EqualTo("No pending document with ID 999 found for the source approver."));
        }

        [Test]
        public async Task ReassignDocuments_NewApproverLevelTooLow_ThrowsArgumentException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var fromUser = new User { Name = "From", Email = "from@test.com", PasswordHash = "hashed", DepartmentId = dept.Id, Level = 1 };
            var toUser = new User { Name = "To", Email = "to@test.com", PasswordHash = "hashed", DepartmentId = dept.Id, Level = 2 };
            var uploader = new User { Name = "Uploader", Email = "up@test.com", PasswordHash = "hashed", DepartmentId = dept.Id, Level = 4 };
            _context.Users.AddRange(fromUser, toUser, uploader);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Important Doc",
                CreatedByUserId = uploader.Id,
                CurrentApproverUserId = fromUser.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var version = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = 1,
                OriginalFileName = "file.txt",
                StoredFileName = "stored.txt",
                MimeType = "text/plain",
                IsCurrentVersion = true,
                UploadedByUserId = uploader.Id
            };
            _context.DocumentVersions.Add(version);
            await _context.SaveChangesAsync();

            var request = new ReassignDocumentsRequestDto
            {
                FromApproverId = fromUser.Id,
                ToApproverId = toUser.Id,
                DocumentId = document.Id
            };

            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _adminService.ReassignDocuments(request, 100));
            Assert.That(ex.Message, Does.Contain("New approver's level (2) is less than the uploader's level (4)"));
        }

        [Test]
        public async Task ReassignDocuments_AllDocuments_SuccessfullyReassigned()
        {
            var deptA = new Department { Name = "HR" };
            var deptB = new Department { Name = "Engineering" };
            _context.Departments.AddRange(deptA, deptB);
            await _context.SaveChangesAsync();

            var fromUser = new User { Name = "From", Email = "from@test.com", PasswordHash = "hashed", DepartmentId = deptA.Id, Level = 1 };
            var toUser = new User { Name = "To", Email = "to@test.com", PasswordHash = "hashed", DepartmentId = deptB.Id, Level = 3 };
            var uploader = new User { Name = "Uploader", Email = "up@test.com", PasswordHash = "hashed", DepartmentId = deptA.Id, Level = 2 };
            _context.Users.AddRange(fromUser, toUser, uploader);
            await _context.SaveChangesAsync();

            var doc1 = new Document
            {
                Title = "Doc 1",
                CreatedByUserId = uploader.Id,
                CurrentApproverUserId = fromUser.Id,
                TargetDepartmentId = deptA.Id,
                DocumentStatus = DocumentStatus.PendingApproval
            };
            var doc2 = new Document
            {
                Title = "Doc 2",
                CreatedByUserId = uploader.Id,
                CurrentApproverUserId = fromUser.Id,
                TargetDepartmentId = deptA.Id,
                DocumentStatus = DocumentStatus.PendingApproval
            };
            _context.Documents.AddRange(doc1, doc2);
            await _context.SaveChangesAsync();

            var v1 = new DocumentVersion { DocumentId = doc1.Id, VersionNumber = 1, IsCurrentVersion = true, UploadedByUserId = uploader.Id };
            var v2 = new DocumentVersion { DocumentId = doc2.Id, VersionNumber = 1, IsCurrentVersion = true, UploadedByUserId = uploader.Id };
            _context.DocumentVersions.AddRange(v1, v2);
            await _context.SaveChangesAsync();

            var request = new ReassignDocumentsRequestDto
            {
                FromApproverId = fromUser.Id,
                ToApproverId = toUser.Id,
                DocumentId = null
            };

            var result = await _adminService.ReassignDocuments(request, 100);

            Assert.That(result, Is.True);

            var updatedDoc1 = await _context.Documents.FindAsync(doc1.Id);
            var updatedDoc2 = await _context.Documents.FindAsync(doc2.Id);

            Assert.That(updatedDoc1!.CurrentApproverUserId, Is.EqualTo(toUser.Id));
            Assert.That(updatedDoc1.TargetDepartmentId, Is.EqualTo(toUser.DepartmentId));

            Assert.That(updatedDoc2!.CurrentApproverUserId, Is.EqualTo(toUser.Id));
            Assert.That(updatedDoc2.TargetDepartmentId, Is.EqualTo(toUser.DepartmentId));

            var logs = await _context.AuditLogs.Where(al => al.PerformedByUserId == 100).ToListAsync();
            Assert.That(logs.Count, Is.EqualTo(2));
            Assert.That(logs.Any(al => al.DocumentId == doc1.Id && al.Action == AuditAction.DocumentForwarded));
            Assert.That(logs.Any(al => al.DocumentId == doc2.Id && al.Action == AuditAction.DocumentForwarded));
        }

        [Test]
        public async Task ReassignDocuments_SpecificDocument_SuccessfullyReassigned()
        {
            var deptA = new Department { Name = "HR" };
            var deptB = new Department { Name = "Engineering" };
            _context.Departments.AddRange(deptA, deptB);
            await _context.SaveChangesAsync();

            var fromUser = new User { Name = "From", Email = "from@test.com", PasswordHash = "hashed", DepartmentId = deptA.Id, Level = 1 };
            var toUser = new User { Name = "To", Email = "to@test.com", PasswordHash = "hashed", DepartmentId = deptB.Id, Level = 3 };
            var uploader = new User { Name = "Uploader", Email = "up@test.com", PasswordHash = "hashed", DepartmentId = deptA.Id, Level = 2 };
            _context.Users.AddRange(fromUser, toUser, uploader);
            await _context.SaveChangesAsync();

            var doc1 = new Document
            {
                Title = "Doc 1",
                CreatedByUserId = uploader.Id,
                CurrentApproverUserId = fromUser.Id,
                TargetDepartmentId = deptA.Id,
                DocumentStatus = DocumentStatus.PendingApproval
            };
            var doc2 = new Document
            {
                Title = "Doc 2",
                CreatedByUserId = uploader.Id,
                CurrentApproverUserId = fromUser.Id,
                TargetDepartmentId = deptA.Id,
                DocumentStatus = DocumentStatus.PendingApproval
            };
            _context.Documents.AddRange(doc1, doc2);
            await _context.SaveChangesAsync();

            var v1 = new DocumentVersion { DocumentId = doc1.Id, VersionNumber = 1, IsCurrentVersion = true, UploadedByUserId = uploader.Id };
            var v2 = new DocumentVersion { DocumentId = doc2.Id, VersionNumber = 1, IsCurrentVersion = true, UploadedByUserId = uploader.Id };
            _context.DocumentVersions.AddRange(v1, v2);
            await _context.SaveChangesAsync();

            var request = new ReassignDocumentsRequestDto
            {
                FromApproverId = fromUser.Id,
                ToApproverId = toUser.Id,
                DocumentId = doc1.Id
            };

            var result = await _adminService.ReassignDocuments(request, 100);

            Assert.That(result, Is.True);

            var updatedDoc1 = await _context.Documents.FindAsync(doc1.Id);
            var updatedDoc2 = await _context.Documents.FindAsync(doc2.Id);

            Assert.That(updatedDoc1!.CurrentApproverUserId, Is.EqualTo(toUser.Id));
            Assert.That(updatedDoc1.TargetDepartmentId, Is.EqualTo(toUser.DepartmentId));

            Assert.That(updatedDoc2!.CurrentApproverUserId, Is.EqualTo(fromUser.Id));
            Assert.That(updatedDoc2.TargetDepartmentId, Is.EqualTo(deptA.Id));

            var logs = await _context.AuditLogs.Where(al => al.PerformedByUserId == 100).ToListAsync();
            Assert.That(logs.Count, Is.EqualTo(1));
            Assert.That(logs.First().DocumentId, Is.EqualTo(doc1.Id));
            Assert.That(logs.First().Action, Is.EqualTo(AuditAction.DocumentForwarded));
        }

        [Test]
        public void DeactivateUser_UserNotFound_ThrowsEntityNotFoundException()
        {
            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _adminService.DeactivateUser(999, 100));
            Assert.That(ex.Message, Is.EqualTo("User with ID 999 was not found."));
        }

        [Test]
        public async Task DeactivateUser_SelfDeactivation_ThrowsArgumentException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var adminUser = new User { Name = "Admin", Email = "admin@test.com", PasswordHash = "hash", DepartmentId = dept.Id, IsAdmin = true };
            _context.Users.Add(adminUser);
            await _context.SaveChangesAsync();

            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _adminService.DeactivateUser(adminUser.Id, adminUser.Id));
            Assert.That(ex.Message, Is.EqualTo("An admin cannot deactivate their own account."));
        }

        [Test]
        public async Task DeactivateUser_DeactivatingAdmin_ThrowsArgumentException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var adminUser = new User { Name = "Admin", Email = "admin@test.com", PasswordHash = "hash", DepartmentId = dept.Id, IsAdmin = true };
            _context.Users.Add(adminUser);
            await _context.SaveChangesAsync();

            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _adminService.DeactivateUser(adminUser.Id, 100));
            Assert.That(ex.Message, Is.EqualTo("Admin accounts cannot be deactivated. Remove admin privileges first."));
        }

        [Test]
        public async Task DeactivateUser_AlreadyDeactivated_ThrowsInvalidOperationException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "User", Email = "user@test.com", PasswordHash = "hash", DepartmentId = dept.Id, IsActive = false };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _adminService.DeactivateUser(user.Id, 100));
            Assert.That(ex.Message, Is.EqualTo("User is already deactivated."));
        }

        [Test]
        public async Task DeactivateUser_UserIsCurrentApprover_ThrowsInvalidOperationException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Approver", Email = "app@test.com", PasswordHash = "hash", DepartmentId = dept.Id, IsActive = true };
            var uploader = new User { Name = "Uploader", Email = "up@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(user, uploader);
            await _context.SaveChangesAsync();

            var doc = new Document
            {
                Title = "Doc",
                CreatedByUserId = uploader.Id,
                CurrentApproverUserId = user.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval
            };
            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _adminService.DeactivateUser(user.Id, 100));
            Assert.That(ex.Message, Does.Contain("User cannot be deactivated: they are the current approver for 1 pending document(s)."));
        }

        [Test]
        public async Task DeactivateUser_UserHasPendingUploadedDocs_ThrowsInvalidOperationException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Uploader", Email = "up@test.com", PasswordHash = "hash", DepartmentId = dept.Id, IsActive = true };
            var approver = new User { Name = "Approver", Email = "app@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(user, approver);
            await _context.SaveChangesAsync();

            var doc = new Document
            {
                Title = "Doc",
                CreatedByUserId = user.Id,
                CurrentApproverUserId = approver.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval
            };
            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _adminService.DeactivateUser(user.Id, 100));
            Assert.That(ex.Message, Does.Contain("User cannot be deactivated: they have 1 document(s) still awaiting approval."));
        }

        [Test]
        public async Task DeactivateUser_UserHasActiveSubordinates_ThrowsInvalidOperationException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var manager = new User { Name = "Manager", Email = "mgr@test.com", PasswordHash = "hash", DepartmentId = dept.Id, IsActive = true };
            _context.Users.Add(manager);
            await _context.SaveChangesAsync();

            var employee = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hash", DepartmentId = dept.Id, IsActive = true, ManagerId = manager.Id };
            _context.Users.Add(employee);
            await _context.SaveChangesAsync();

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _adminService.DeactivateUser(manager.Id, 100));
            Assert.That(ex.Message, Does.Contain("User cannot be deactivated: they are listed as the manager for 1 active user(s)."));
        }

        [Test]
        public async Task DeactivateUser_SuccessfulDeactivation()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "User To Deactivate", Email = "user@test.com", PasswordHash = "hash", DepartmentId = dept.Id, IsActive = true };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await _adminService.DeactivateUser(user.Id, 100);

            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.That(updatedUser!.IsActive, Is.False);

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(al => al.PerformedByUserId == 100);
            Assert.That(auditLog, Is.Not.Null);
            Assert.That(auditLog.Action, Is.EqualTo(AuditAction.UserDeactivated));
            Assert.That(auditLog.Details, Is.EqualTo($"Admin deactivated user '{user.Name}' (Email: {user.Email})."));
        }

        [Test]
        public void ReactivateUser_UserNotFound_ThrowsEntityNotFoundException()
        {
            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _adminService.ReactivateUser(999, 100));
            Assert.That(ex.Message, Is.EqualTo("User with ID 999 was not found."));
        }

        [Test]
        public async Task ReactivateUser_UserAlreadyActive_ThrowsInvalidOperationException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "User", Email = "user@test.com", PasswordHash = "hash", DepartmentId = dept.Id, IsActive = true };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _adminService.ReactivateUser(user.Id, 100));
            Assert.That(ex.Message, Is.EqualTo("User is already active."));
        }

        [Test]
        public async Task ReactivateUser_SuccessfulReactivation()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "User To Reactivate", Email = "user@test.com", PasswordHash = "hash", DepartmentId = dept.Id, IsActive = false };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await _adminService.ReactivateUser(user.Id, 100);

            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.That(updatedUser!.IsActive, Is.True);

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(al => al.PerformedByUserId == 100);
            Assert.That(auditLog, Is.Not.Null);
            Assert.That(auditLog.Action, Is.EqualTo(AuditAction.UserReactivated));
            Assert.That(auditLog.Details, Is.EqualTo($"Admin reactivated user '{user.Name}' (Email: {user.Email})."));
        }

        [Test]
        public void RejectAllPendingDocumentsOfUser_UserNotFound_ThrowsEntityNotFoundException()
        {
            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _adminService.RejectAllPendingDocumentsOfUser(999, 100, "Reason"));
            Assert.That(ex.Message, Is.EqualTo("User with ID 999 was not found."));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public async Task RejectAllPendingDocumentsOfUser_EmptyReason_ThrowsArgumentException(string? reason)
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "User", Email = "user@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _adminService.RejectAllPendingDocumentsOfUser(user.Id, 100, reason!));
            Assert.That(ex.Message, Is.EqualTo("Reason is required."));
        }

        [Test]
        public async Task RejectAllPendingDocumentsOfUser_UserHasNoPendingDocuments_ReturnsEarly()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "User", Email = "user@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            Assert.DoesNotThrowAsync(async () =>
                await _adminService.RejectAllPendingDocumentsOfUser(user.Id, 100, "Some reason"));

            var approvalActions = await _context.ApprovalActions.ToListAsync();
            Assert.That(approvalActions, Is.Empty);

            var auditLogs = await _context.AuditLogs.ToListAsync();
            Assert.That(auditLogs, Is.Empty);
        }

        [Test]
        public async Task RejectAllPendingDocumentsOfUser_SuccessfulRejection()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "User", Email = "user@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var approver = new User { Name = "Approver", Email = "app@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(user, approver);
            await _context.SaveChangesAsync();

            var doc1 = new Document
            {
                Title = "Doc 1",
                CreatedByUserId = user.Id,
                CurrentApproverUserId = approver.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval
            };
            var doc2 = new Document
            {
                Title = "Doc 2",
                CreatedByUserId = user.Id,
                CurrentApproverUserId = null,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.Approved
            };
            _context.Documents.AddRange(doc1, doc2);
            await _context.SaveChangesAsync();

            var v1 = new DocumentVersion { DocumentId = doc1.Id, VersionNumber = 1, IsCurrentVersion = true, UploadedByUserId = user.Id };
            var v2 = new DocumentVersion { DocumentId = doc2.Id, VersionNumber = 1, IsCurrentVersion = true, UploadedByUserId = user.Id };
            _context.DocumentVersions.AddRange(v1, v2);
            await _context.SaveChangesAsync();

            await _adminService.RejectAllPendingDocumentsOfUser(user.Id, 100, "Violation of policy");

            var updatedDoc1 = await _context.Documents.FindAsync(doc1.Id);
            Assert.That(updatedDoc1!.DocumentStatus, Is.EqualTo(DocumentStatus.Rejected));
            Assert.That(updatedDoc1.CurrentApproverUserId, Is.Null);

            var updatedDoc2 = await _context.Documents.FindAsync(doc2.Id);
            Assert.That(updatedDoc2!.DocumentStatus, Is.EqualTo(DocumentStatus.Approved));

            var action = await _context.ApprovalActions.FirstOrDefaultAsync(aa => aa.DocumentId == doc1.Id);
            Assert.That(action, Is.Not.Null);
            Assert.That(action.Action, Is.EqualTo(ApprovalActionType.Rejected));
            Assert.That(action.Comments, Is.EqualTo("Violation of policy"));
            Assert.That(action.ApproverUserId, Is.EqualTo(100));

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(al => al.DocumentId == doc1.Id);
            Assert.That(auditLog, Is.Not.Null);
            Assert.That(auditLog.Action, Is.EqualTo(AuditAction.DocumentRejected));
            Assert.That(auditLog.Details, Is.EqualTo("Document rejected by administrator. Reason: Violation of policy"));
            Assert.That(auditLog.PerformedByUserId, Is.EqualTo(100));
        }

        [Test]
        public void BulkUploadUsersAsync_FileNull_ThrowsArgumentException()
        {
            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _adminService.BulkUploadUsersAsync(null!, 100));
            Assert.That(ex.Message, Is.EqualTo("CSV file is required."));
        }

        [Test]
        public void BulkUploadUsersAsync_FileLengthZero_ThrowsArgumentException()
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(_ => _.Length).Returns(0);

            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _adminService.BulkUploadUsersAsync(fileMock.Object, 100));
            Assert.That(ex.Message, Is.EqualTo("CSV file is required."));
        }

        [Test]
        public void BulkUploadUsersAsync_InvalidContentType_ThrowsArgumentException()
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(_ => _.Length).Returns(100);
            fileMock.Setup(_ => _.ContentType).Returns("application/pdf");

            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _adminService.BulkUploadUsersAsync(fileMock.Object, 100));
            Assert.That(ex.Message, Is.EqualTo("Only .csv files are accepted."));
        }

        [Test]
        public async Task BulkUploadUsersAsync_InvalidRows_LogsErrorsInResult()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var existingUser = new User { Name = "Existing", Email = "existing@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(existingUser);
            await _context.SaveChangesAsync();

            var csvContent = new StringBuilder();
            csvContent.AppendLine("Name,Email,Password,DepartmentId");
            csvContent.AppendLine("Too,Short"); // Row 2: < 4 columns
            csvContent.AppendLine(",email1@test.com,pass123," + dept.Id); // Row 3: empty name
            csvContent.AppendLine("Name,,pass123," + dept.Id); // Row 4: empty email
            csvContent.AppendLine("Name,email2@test.com,123," + dept.Id); // Row 5: password < 6 chars
            csvContent.AppendLine("Name,email3@test.com,pass123,abc"); // Row 6: invalid dept ID (non-int)
            csvContent.AppendLine("Name,email4@test.com,pass123,999"); // Row 7: non-existent dept ID
            csvContent.AppendLine("Name,existing@test.com,pass123," + dept.Id); // Row 8: existing email

            var fileMock = new Mock<IFormFile>();
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(csvContent.ToString()));
            fileMock.Setup(_ => _.OpenReadStream()).Returns(ms);
            fileMock.Setup(_ => _.Length).Returns(ms.Length);
            fileMock.Setup(_ => _.ContentType).Returns("text/csv");

            var result = await _adminService.BulkUploadUsersAsync(fileMock.Object, 100);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.TotalRows, Is.EqualTo(7));
            Assert.That(result.SuccessCount, Is.EqualTo(0));
            Assert.That(result.FailureCount, Is.EqualTo(7));

            Assert.That(result.Results[0].RowNumber, Is.EqualTo(2));
            Assert.That(result.Results[0].Success, Is.False);
            Assert.That(result.Results[0].Error, Does.Contain("does not have all 4 required columns"));

            Assert.That(result.Results[1].RowNumber, Is.EqualTo(3));
            Assert.That(result.Results[1].Success, Is.False);
            Assert.That(result.Results[1].Error, Is.EqualTo("Name is required."));

            Assert.That(result.Results[2].RowNumber, Is.EqualTo(4));
            Assert.That(result.Results[2].Success, Is.False);
            Assert.That(result.Results[2].Error, Is.EqualTo("Email is required."));

            Assert.That(result.Results[3].RowNumber, Is.EqualTo(5));
            Assert.That(result.Results[3].Success, Is.False);
            Assert.That(result.Results[3].Error, Is.EqualTo("Password must be at least 6 characters."));

            Assert.That(result.Results[4].RowNumber, Is.EqualTo(6));
            Assert.That(result.Results[4].Success, Is.False);
            Assert.That(result.Results[4].Error, Is.EqualTo("Invalid DepartmentId 'abc'."));

            Assert.That(result.Results[5].RowNumber, Is.EqualTo(7));
            Assert.That(result.Results[5].Success, Is.False);
            Assert.That(result.Results[5].Error, Is.EqualTo("Department with ID 999 was not found."));

            Assert.That(result.Results[6].RowNumber, Is.EqualTo(8));
            Assert.That(result.Results[6].Success, Is.False);
            Assert.That(result.Results[6].Error, Is.EqualTo("A user with email 'existing@test.com' already exists."));
        }

        [Test]
        public async Task BulkUploadUsersAsync_ValidCsv_SuccessfullyImportsUsers()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var csvContent = new StringBuilder();
            csvContent.AppendLine("Name,Email,Password,DepartmentId");
            csvContent.AppendLine("New User 1,new1@test.com,password123," + dept.Id);
            csvContent.AppendLine("New User 2,new2@test.com,password456," + dept.Id);

            var fileMock = new Mock<IFormFile>();
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(csvContent.ToString()));
            fileMock.Setup(_ => _.OpenReadStream()).Returns(ms);
            fileMock.Setup(_ => _.Length).Returns(ms.Length);
            fileMock.Setup(_ => _.ContentType).Returns("text/csv");

            var result = await _adminService.BulkUploadUsersAsync(fileMock.Object, 100);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.TotalRows, Is.EqualTo(2));
            Assert.That(result.SuccessCount, Is.EqualTo(2));
            Assert.That(result.FailureCount, Is.EqualTo(0));

            var u1 = await _context.Users.FirstOrDefaultAsync(u => u.Email == "new1@test.com");
            Assert.That(u1, Is.Not.Null);
            Assert.That(u1!.Name, Is.EqualTo("New User 1"));
            Assert.That(u1.DepartmentId, Is.EqualTo(dept.Id));
            Assert.That(u1.PasswordHash, Is.Not.EqualTo("password123")); // verified hashed

            var u2 = await _context.Users.FirstOrDefaultAsync(u => u.Email == "new2@test.com");
            Assert.That(u2, Is.Not.Null);
            Assert.That(u2!.Name, Is.EqualTo("New User 2"));

            // Verify Audit Logs are created
            var logs = await _context.AuditLogs.Where(al => al.PerformedByUserId == 100).ToListAsync();
            Assert.That(logs.Count, Is.EqualTo(2));
            Assert.That(logs.Any(al => al.Action == AuditAction.UserRegistered && al.Details!.Contains("new1@test.com")));
            Assert.That(logs.Any(al => al.Action == AuditAction.UserRegistered && al.Details!.Contains("new2@test.com")));
        }
    }
}