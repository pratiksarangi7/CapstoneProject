using Microsoft.EntityFrameworkCore;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Models.Enums;
using CapstoneProjectAPI.Exceptions;

namespace CapstoneProjectTest
{
    public partial class DocumentServiceTests
    {
        [Test]
        public async Task UploadDocument_SameDepartment_WithManager_SetsManagerAsApprover()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var manager = new User { Name = "Manager User", Email = "manager@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(manager);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader User", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id, ManagerId = manager.Id };
            _context.Users.Add(uploader);
            await _context.SaveChangesAsync();

            var mockFile = CreateMockFile("test.pdf", "application/pdf", 100);

            var request = new UploadDocumentRequestDto
            {
                Title = "Test Document",
                Description = "Description of test document",
                TargetDepartmentId = dept.Id,
                File = mockFile.Object
            };

            var result = await _documentService.UploadDocument(request, uploader.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.DocumentId, Is.GreaterThan(0));
            Assert.That(result.DocumentStatus, Is.EqualTo("PendingApproval"));
            Assert.That(result.CurrentApproverUserId, Is.EqualTo(manager.Id));
            Assert.That(result.CurrentApproverName, Is.EqualTo("Manager User"));
            Assert.That(result.VersionNumber, Is.EqualTo(1));
            Assert.That(result.OriginalFileName, Is.EqualTo("test.pdf"));

            var fileCreated = Directory.GetFiles(_testUploadsFolder).Length > 0;
            Assert.That(fileCreated, Is.True);
        }

        [Test]
        public async Task UploadDocument_DifferentDepartment_FindsApproverWithSameLevel()
        {
            var deptA = new Department { Name = "HR" };
            var deptB = new Department { Name = "Engineering" };
            _context.Departments.AddRange(deptA, deptB);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = deptA.Id, Level = 2 };
            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = deptB.Id, Level = 2 };
            _context.Users.AddRange(uploader, approver);
            await _context.SaveChangesAsync();

            var mockFile = CreateMockFile("test.png", "image/png", 500);

            var request = new UploadDocumentRequestDto
            {
                Title = "Cross-Dept Document",
                TargetDepartmentId = deptB.Id,
                File = mockFile.Object
            };

            var result = await _documentService.UploadDocument(request, uploader.Id);

            Assert.That(result.CurrentApproverUserId, Is.EqualTo(approver.Id));
            Assert.That(result.DocumentStatus, Is.EqualTo("PendingApproval"));
        }

        [Test]
        public async Task UploadDocument_DifferentDepartment_NoApprover_ThrowsInvalidOperationException()
        {
            var deptA = new Department { Name = "HR" };
            var deptB = new Department { Name = "Engineering" };
            _context.Departments.AddRange(deptA, deptB);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = deptA.Id, Level = 2 };
            var approver = new User { Name = "LowLevelApprover", Email = "low@test.com", PasswordHash = "hash", DepartmentId = deptB.Id, Level = 1 };
            _context.Users.AddRange(uploader, approver);
            await _context.SaveChangesAsync();

            var mockFile = CreateMockFile("test.png", "image/png", 500);

            var request = new UploadDocumentRequestDto
            {
                Title = "Cross-Dept Document",
                TargetDepartmentId = deptB.Id,
                File = mockFile.Object
            };

            Assert.ThrowsAsync<InvalidOperationException>(async () => await _documentService.UploadDocument(request, uploader.Id));
        }

        [Test]
        public async Task UploadDocument_FileExceedsMaxSize_ThrowsArgumentException()
        {
            var mockFile = CreateMockFile("large.pdf", "application/pdf", 6 * 1024 * 1024);

            var request = new UploadDocumentRequestDto
            {
                Title = "Large Doc",
                TargetDepartmentId = 1,
                File = mockFile.Object
            };

            Assert.ThrowsAsync<ArgumentException>(async () => await _documentService.UploadDocument(request, 1));
        }

        [Test]
        public async Task UploadDocument_InvalidMimeType_ThrowsArgumentException()
        {
            var mockFile = CreateMockFile("test.txt", "text/plain", 100);

            var request = new UploadDocumentRequestDto
            {
                Title = "Text Doc",
                TargetDepartmentId = 1,
                File = mockFile.Object
            };

            Assert.ThrowsAsync<ArgumentException>(async () => await _documentService.UploadDocument(request, 1));
        }

        [Test]
        public async Task WithdrawDocumentAsync_ValidRequest_DeletesDocumentAndVersionsAndClearsAuditLogs()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var manager = new User { Name = "Manager", Email = "manager@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(manager);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id, ManagerId = manager.Id };
            _context.Users.Add(uploader);
            await _context.SaveChangesAsync();

            var mockFile = CreateMockFile("withdraw.pdf", "application/pdf", 100);
            var uploadRequest = new UploadDocumentRequestDto
            {
                Title = "Document to Withdraw",
                TargetDepartmentId = dept.Id,
                File = mockFile.Object
            };

            var uploadedDoc = await _documentService.UploadDocument(uploadRequest, uploader.Id);

            _context.AuditLogs.Add(new AuditLog
            {
                Action = AuditAction.DocumentUploaded,
                PerformedByUserId = uploader.Id,
                DocumentId = uploadedDoc.DocumentId,
                Details = "Audit test"
            });
            await _context.SaveChangesAsync();

            var files = Directory.GetFiles(_testUploadsFolder);
            Assert.That(files.Length, Is.EqualTo(1));

            await _documentService.WithdrawDocumentAsync(uploadedDoc.DocumentId, uploader.Id);

            var docExists = await _context.Documents.AnyAsync(d => d.Id == uploadedDoc.DocumentId);
            Assert.That(docExists, Is.False);

            var auditLogExists = await _context.AuditLogs.AnyAsync(al => al.DocumentId == uploadedDoc.DocumentId);
            Assert.That(auditLogExists, Is.False);

            var remainingFiles = Directory.GetFiles(_testUploadsFolder);
            Assert.That(remainingFiles.Length, Is.EqualTo(0));
        }

        [Test]
        public async Task WithdrawDocumentAsync_UserNotAuthorised_ThrowsUnauthorizedAccessException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var manager = new User { Name = "Manager", Email = "manager@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(manager);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id, ManagerId = manager.Id };
            var otherUser = new User { Name = "Other", Email = "other@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(uploader, otherUser);
            await _context.SaveChangesAsync();

            var mockFile = CreateMockFile("withdraw.pdf", "application/pdf", 100);
            var uploadRequest = new UploadDocumentRequestDto
            {
                Title = "Document to Withdraw",
                TargetDepartmentId = dept.Id,
                File = mockFile.Object
            };

            var uploadedDoc = await _documentService.UploadDocument(uploadRequest, uploader.Id);
            Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _documentService.WithdrawDocumentAsync(uploadedDoc.DocumentId, otherUser.Id));
        }

        [Test]
        public async Task WithdrawDocumentAsync_DocumentNotPendingApproval_ThrowsInvalidOperationException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();
            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(uploader);
            await _context.SaveChangesAsync();

            var mockFile = CreateMockFile("withdraw.pdf", "application/pdf", 100);
            var uploadRequest = new UploadDocumentRequestDto
            {
                Title = "Auto-Approved Document",
                TargetDepartmentId = dept.Id,
                File = mockFile.Object
            };

            var uploadedDoc = await _documentService.UploadDocument(uploadRequest, uploader.Id);
            Assert.That(uploadedDoc.DocumentStatus, Is.EqualTo("Approved"));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _documentService.WithdrawDocumentAsync(uploadedDoc.DocumentId, uploader.Id));
        }

        [Test]
        public async Task GetDocumentsUploadedByUserAsync_ValidUser_ReturnsPagedDocuments()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            for (int i = 1; i <= 3; i++)
            {
                _context.Documents.Add(new Document
                {
                    Title = $"Uploaded Doc {i}",
                    CreatedByUserId = user.Id,
                    TargetDepartmentId = dept.Id,
                    DocumentStatus = DocumentStatus.PendingApproval,
                    CreatedAt = DateTime.UtcNow.AddMinutes(i)
                });
            }
            await _context.SaveChangesAsync();

            var result = await _documentService.GetDocumentsUploadedByUserAsync(user.Id, pageNumber: 1, pageSize: 2);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.TotalCount, Is.EqualTo(3));
            Assert.That(result.Items.Count, Is.EqualTo(2));
            Assert.That(result.Items.First().Title, Is.EqualTo("Uploaded Doc 3"));
        }

        [Test]
        public void GetDocumentsUploadedByUserAsync_UserNotFound_ThrowsEntityNotFoundException()
        {
            Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.GetDocumentsUploadedByUserAsync(999, 1, 10));
        }

        [Test]
        public async Task ReUploadDocumentVersionAsync_ValidRequest_CreatesNewVersionSetsPendingApprovalAndWritesAuditLog()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var manager = new User { Name = "Manager", Email = "manager@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(manager);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id, ManagerId = manager.Id };
            _context.Users.Add(uploader);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Rejected Document",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.Rejected,
                CurrentApproverUserId = null
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var oldVersion = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = 1,
                OriginalFileName = "old.pdf",
                StoredFileName = "old_stored.pdf",
                MimeType = "application/pdf",
                IsCurrentVersion = true,
                UploadedByUserId = uploader.Id
            };
            _context.DocumentVersions.Add(oldVersion);
            await _context.SaveChangesAsync();

            var mockFile = CreateMockFile("new.pdf", "application/pdf", 200);
            var request = new ReUploadDocumentRequestDto
            {
                File = mockFile.Object
            };

            var result = await _documentService.ReUploadDocumentVersionAsync(document.Id, request, uploader.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.DocumentId, Is.EqualTo(document.Id));
            Assert.That(result.DocumentStatus, Is.EqualTo("PendingApproval"));
            Assert.That(result.VersionNumber, Is.EqualTo(2));
            Assert.That(result.OriginalFileName, Is.EqualTo("new.pdf"));
            Assert.That(result.CurrentApproverUserId, Is.EqualTo(manager.Id));

            var updatedDoc = await _context.Documents
                .Include(d => d.Versions)
                .FirstOrDefaultAsync(d => d.Id == document.Id);
            Assert.That(updatedDoc, Is.Not.Null);
            Assert.That(updatedDoc!.DocumentStatus, Is.EqualTo(DocumentStatus.PendingApproval));
            Assert.That(updatedDoc.CurrentApproverUserId, Is.EqualTo(manager.Id));
            Assert.That(updatedDoc.Versions.Count, Is.EqualTo(2));

            var dbOldVersion = updatedDoc.Versions.First(v => v.VersionNumber == 1);
            var dbNewVersion = updatedDoc.Versions.First(v => v.VersionNumber == 2);
            Assert.That(dbOldVersion.IsCurrentVersion, Is.False);
            Assert.That(dbNewVersion.IsCurrentVersion, Is.True);
            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(al => al.DocumentId == document.Id && al.Action == AuditAction.NewVersionUploaded);
            Assert.That(auditLog, Is.Not.Null);
            Assert.That(auditLog!.Details, Contains.Substring("New version 2 uploaded"));

            var files = Directory.GetFiles(_testUploadsFolder);
            Assert.That(files.Length, Is.EqualTo(1));
        }

        [Test]
        public void ReUploadDocumentVersionAsync_DocumentNotFound_ThrowsEntityNotFoundException()
        {
            var mockFile = CreateMockFile("new.pdf", "application/pdf", 200);
            var request = new ReUploadDocumentRequestDto { File = mockFile.Object };

            Assert.ThrowsAsync<EntityNotFoundException>(async () => 
                await _documentService.ReUploadDocumentVersionAsync(999, request, 1));
        }

        [Test]
        public async Task ReUploadDocumentVersionAsync_UserNotAuthorised_ThrowsUnauthorizedAccessException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var otherUser = new User { Name = "Other", Email = "other@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(uploader, otherUser);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Rejected Document",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.Rejected
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var mockFile = CreateMockFile("new.pdf", "application/pdf", 200);
            var request = new ReUploadDocumentRequestDto { File = mockFile.Object };

            Assert.ThrowsAsync<UnauthorizedAccessException>(async () => 
                await _documentService.ReUploadDocumentVersionAsync(document.Id, request, otherUser.Id));
        }

        [Test]
        public async Task ReUploadDocumentVersionAsync_DocumentNotRejected_ThrowsInvalidOperationException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(uploader);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Pending Document",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var mockFile = CreateMockFile("new.pdf", "application/pdf", 200);
            var request = new ReUploadDocumentRequestDto { File = mockFile.Object };
            Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await _documentService.ReUploadDocumentVersionAsync(document.Id, request, uploader.Id));
        }

        [Test]
        public async Task ReUploadDocumentVersionAsync_FileExceedsMaxSize_ThrowsArgumentException()
        {
            var mockFile = CreateMockFile("large.pdf", "application/pdf", 6 * 1024 * 1024);
            var request = new ReUploadDocumentRequestDto { File = mockFile.Object };
            Assert.ThrowsAsync<ArgumentException>(async () => 
                await _documentService.ReUploadDocumentVersionAsync(1, request, 1));
        }

        [Test]
        public async Task ReUploadDocumentVersionAsync_InvalidMimeType_ThrowsArgumentException()
        {
            var mockFile = CreateMockFile("test.txt", "text/plain", 100);
            var request = new ReUploadDocumentRequestDto { File = mockFile.Object };
            Assert.ThrowsAsync<ArgumentException>(async () => 
                await _documentService.ReUploadDocumentVersionAsync(1, request, 1));
        }
    }
}