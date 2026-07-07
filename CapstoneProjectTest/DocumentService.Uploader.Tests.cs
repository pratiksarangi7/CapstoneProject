using Microsoft.EntityFrameworkCore;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Models.Enums;
using CapstoneProjectAPI.Exceptions;
using System.Text;
using Moq;
using Microsoft.AspNetCore.Http;
using System.IO;

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
        public async Task WithdrawDocumentAsync_ValidRequest_MarksDocumentAsWithdrawnAndLogsAction()
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

            var doc = await _context.Documents.FindAsync(uploadedDoc.DocumentId);
            Assert.That(doc, Is.Not.Null);
            Assert.That(doc!.DocumentStatus, Is.EqualTo(DocumentStatus.DocumentWithdrawn));
            Assert.That(doc.CurrentApproverUserId, Is.Null);

            var uploadAuditLogExists = await _context.AuditLogs.AnyAsync(al => al.DocumentId == uploadedDoc.DocumentId && al.Action == AuditAction.DocumentUploaded);
            Assert.That(uploadAuditLogExists, Is.True);

            var withdrawAuditLogExists = await _context.AuditLogs.AnyAsync(al => al.PerformedByUserId == uploader.Id && al.Action == AuditAction.DocumentWithdrawn);
            Assert.That(withdrawAuditLogExists, Is.True);

            var remainingFiles = Directory.GetFiles(_testUploadsFolder);
            Assert.That(remainingFiles.Length, Is.EqualTo(1));
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
        public void WithdrawDocumentAsync_DocumentNotFound_ThrowsEntityNotFoundException()
        {
            Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.WithdrawDocumentAsync(9999, 1));
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
        public async Task GetDocumentsUploadedByUserAsync_WithSearch_ReturnsMatchingDocuments()
        {
            var dept1 = new Department { Name = "HR" };
            var dept2 = new Department { Name = "IT" };
            _context.Departments.AddRange(dept1, dept2);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept1.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var doc1 = new Document
            {
                Title = "Financial Report",
                CreatedByUserId = user.Id,
                TargetDepartmentId = dept1.Id,
                DocumentStatus = DocumentStatus.PendingApproval
            };
            var doc2 = new Document
            {
                Title = "Vacation Request",
                CreatedByUserId = user.Id,
                TargetDepartmentId = dept2.Id,
                DocumentStatus = DocumentStatus.Approved
            };
            _context.Documents.AddRange(doc1, doc2);
            await _context.SaveChangesAsync();

            // Search by Title
            var resTitle = await _documentService.GetDocumentsUploadedByUserAsync(user.Id, pageNumber: 1, pageSize: 10, search: "financial");
            Assert.That(resTitle.TotalCount, Is.EqualTo(1));
            Assert.That(resTitle.Items[0].Title, Is.EqualTo("Financial Report"));

            // Search by CreatedByUser Name
            var resUser = await _documentService.GetDocumentsUploadedByUserAsync(user.Id, pageNumber: 1, pageSize: 10, search: "uploader");
            Assert.That(resUser.TotalCount, Is.EqualTo(2));

            // Search by TargetDepartment Name
            var resDept = await _documentService.GetDocumentsUploadedByUserAsync(user.Id, pageNumber: 1, pageSize: 10, search: "it");
            Assert.That(resDept.TotalCount, Is.EqualTo(1));
            Assert.That(resDept.Items[0].Title, Is.EqualTo("Vacation Request"));

            // Search with no matches
            var resEmpty = await _documentService.GetDocumentsUploadedByUserAsync(user.Id, pageNumber: 1, pageSize: 10, search: "nomatch");
            Assert.That(resEmpty.TotalCount, Is.EqualTo(0));
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

        [Test]
        public void UploadDocument_FileIsNull_ThrowsArgumentException()
        {
            var request = new UploadDocumentRequestDto
            {
                Title = "Test Document",
                Description = "Description",
                TargetDepartmentId = 1,
                File = null!
            };

            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _documentService.UploadDocument(request, 1));
            Assert.That(ex.Message, Is.EqualTo("File is required."));
        }

        [Test]
        public void UploadDocument_FileLengthZero_ThrowsArgumentException()
        {
            var mockFile = CreateMockFile("empty.pdf", "application/pdf", 0);
            var request = new UploadDocumentRequestDto
            {
                Title = "Test Document",
                Description = "Description",
                TargetDepartmentId = 1,
                File = mockFile.Object
            };

            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _documentService.UploadDocument(request, 1));
            Assert.That(ex.Message, Is.EqualTo("File is required."));
        }

        [Test]
        public async Task UploadDocument_UploaderNull_ThrowsEntityNotFoundException()
        {
            var mockFile = CreateMockFile("test.pdf", "application/pdf", 100);
            var request = new UploadDocumentRequestDto
            {
                Title = "Test Document",
                Description = "Description",
                TargetDepartmentId = 1,
                File = mockFile.Object
            };

            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.UploadDocument(request, 999));
            Assert.That(ex.Message, Is.EqualTo("Uploader user not found."));
        }

        [Test]
        public async Task UploadDocument_TargetDepartmentDoesNotExist_ThrowsEntityNotFoundException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User 
            { 
                Name = "Uploader User", 
                Email = "uploader@test.com", 
                PasswordHash = "hash", 
                DepartmentId = dept.Id 
            };
            _context.Users.Add(uploader);
            await _context.SaveChangesAsync();

            var mockFile = CreateMockFile("test.pdf", "application/pdf", 100);
            var request = new UploadDocumentRequestDto
            {
                Title = "Test Document",
                Description = "Description",
                TargetDepartmentId = 999,
                File = mockFile.Object
            };

            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.UploadDocument(request, uploader.Id));
            Assert.That(ex.Message, Is.EqualTo("Target department not found."));
        }

        [Test]
        public async Task UploadDocument_DuplicateTitle_ThrowsInvalidOperationException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User 
            { 
                Name = "Uploader User", 
                Email = "uploader@test.com", 
                PasswordHash = "hash", 
                DepartmentId = dept.Id,
                Level = 2
            };
            var manager = new User
            {
                Name = "Manager User",
                Email = "manager@test.com",
                PasswordHash = "hash",
                DepartmentId = dept.Id,
                Level = 3
            };
            _context.Users.AddRange(uploader, manager);
            await _context.SaveChangesAsync();

            var activeDoc = new Document
            {
                Title = "Active Doc",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval
            };
            _context.Documents.Add(activeDoc);
            await _context.SaveChangesAsync();

            var mockFile = CreateMockFile("test.pdf", "application/pdf", 100);
            var request = new UploadDocumentRequestDto
            {
                Title = "Active Doc",
                Description = "Description",
                TargetDepartmentId = dept.Id,
                File = mockFile.Object
            };

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _documentService.UploadDocument(request, uploader.Id));
            Assert.That(ex.Message, Does.Contain("A document with the same title is already active"));
        }

        [Test]
        public async Task GetDocumentFileAsync_NoVersionsExist_ThrowsEntityNotFoundException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(uploader);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Document",
                Description = "Description",
                CreatedByUserId = uploader.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                TargetDepartmentId = dept.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.GetDocumentFileAsync(document.Id, uploader.Id));
            Assert.That(ex.Message, Is.EqualTo($"No version found for document {document.Id}."));
        }

        [Test]
        public async Task GetDocumentFileAsync_NoCurrentVersionSet_FallsBackToHighestVersionNumber()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(uploader);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Document",
                Description = "Description",
                CreatedByUserId = uploader.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                TargetDepartmentId = dept.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var version1 = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = 1,
                OriginalFileName = "version1.pdf",
                StoredFileName = "stored_version1.pdf",
                MimeType = "application/pdf",
                IsCurrentVersion = false,
                UploadedByUserId = uploader.Id
            };
            var version2 = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = 2,
                OriginalFileName = "version2.pdf",
                StoredFileName = "stored_version2.pdf",
                MimeType = "application/pdf",
                IsCurrentVersion = false,
                UploadedByUserId = uploader.Id
            };
            _context.DocumentVersions.AddRange(version1, version2);
            await _context.SaveChangesAsync();

            Directory.CreateDirectory(_testUploadsFolder);
            string physicalPath = Path.Combine(_testUploadsFolder, "stored_version2.pdf");
            await File.WriteAllTextAsync(physicalPath, "dummy file contents");

            var result = await _documentService.GetDocumentFileAsync(document.Id, uploader.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.VersionNumber, Is.EqualTo(2));
            Assert.That(result.OriginalFileName, Is.EqualTo("version2.pdf"));
            Assert.That(result.FilePath, Is.EqualTo(physicalPath));
        }

        [Test]
        public async Task GetDocumentsUploadedByUserAsync_WithVersionsAndApprovalActions_TriggersMappingCorrectly()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Uploader User", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var approver = new User { Name = "Approver User", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(user, approver);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Doc with Version",
                CreatedByUserId = user.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CreatedAt = DateTime.UtcNow
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var version = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = 1,
                OriginalFileName = "test.pdf",
                StoredFileName = "stored.pdf",
                FileSize = 100,
                MimeType = "application/pdf",
                IsCurrentVersion = true,
                UploadedByUserId = user.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.DocumentVersions.Add(version);
            await _context.SaveChangesAsync();

            var action = new ApprovalAction
            {
                DocumentId = document.Id,
                DocumentVersionId = version.Id,
                ApproverUserId = approver.Id,
                Action = ApprovalActionType.Approved,
                Comments = "Looks perfect",
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.ApprovalActions.Add(action);
            await _context.SaveChangesAsync();

            var result = await _documentService.GetDocumentsUploadedByUserAsync(user.Id, pageNumber: 1, pageSize: 10);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Items.Count, Is.EqualTo(1));
            var mappedDoc = result.Items.First();
            Assert.That(mappedDoc.Versions.Count, Is.EqualTo(1));
            
            var mappedVersion = mappedDoc.Versions.First();
            Assert.That(mappedVersion.Id, Is.EqualTo(version.Id));
            Assert.That(mappedVersion.OriginalFileName, Is.EqualTo("test.pdf"));
            Assert.That(mappedVersion.UploadedByUserName, Is.EqualTo("Uploader User"));

            Assert.That(mappedVersion.ApprovalAction, Is.Not.Null);
            Assert.That(mappedVersion.ApprovalAction!.Action, Is.EqualTo("Approved"));
            Assert.That(mappedVersion.ApprovalAction.Comments, Is.EqualTo("Looks perfect"));
            Assert.That(mappedVersion.ApprovalAction.ApproverUserName, Is.EqualTo("Approver User"));
        }

        [Test]
        public async Task GetDocumentFileAsync_UserNotAuthorizedToView_ThrowsUnauthorizedAccessException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var unauthorizedUser = new User { Name = "Unauthorized", Email = "unauth@test.com", PasswordHash = "hash", DepartmentId = dept.Id, IsAdmin = false };
            _context.Users.AddRange(uploader, unauthorizedUser);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Confidential Document",
                Description = "Description",
                CreatedByUserId = uploader.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CurrentApproverUserId = null,
                TargetDepartmentId = dept.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _documentService.GetDocumentFileAsync(document.Id, unauthorizedUser.Id));
            Assert.That(ex.Message, Is.EqualTo("You are not authorised to view this document."));
        }

        [Test]
        public void GetDocumentFileAsync_DocumentDoesNotExist_ThrowsEntityNotFoundException()
        {
            Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.GetDocumentFileAsync(9999, 1));
        }

        [Test]
        public async Task GetDocumentFileAsync_RequestingUserDoesNotExist_ThrowsEntityNotFoundException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(uploader);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Document",
                Description = "Description",
                CreatedByUserId = uploader.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                TargetDepartmentId = dept.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.GetDocumentFileAsync(document.Id, 9999));
            Assert.That(ex.Message, Is.EqualTo("Requesting user not found."));
        }

        [Test]
        public async Task GetDocumentFileAsync_SpecifiedVersionIdNotFound_ThrowsEntityNotFoundException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(uploader);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Document",
                Description = "Description",
                CreatedByUserId = uploader.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                TargetDepartmentId = dept.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var version = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = 1,
                OriginalFileName = "test.pdf",
                StoredFileName = "stored.pdf",
                MimeType = "application/pdf",
                IsCurrentVersion = true,
                UploadedByUserId = uploader.Id
            };
            _context.DocumentVersions.Add(version);
            await _context.SaveChangesAsync();

            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.GetDocumentFileAsync(document.Id, uploader.Id, versionId: 9999));
            Assert.That(ex.Message, Is.EqualTo($"Version with ID 9999 was not found on document {document.Id}."));
        }

        [Test]
        public async Task UploadDocument_DifferentDepartment_NoSameLevelApprover_FindsHigherLevelApprover()
        {
            var deptA = new Department { Name = "HR" };
            var deptB = new Department { Name = "Engineering" };
            _context.Departments.AddRange(deptA, deptB);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = deptA.Id, Level = 2 };
            var highApprover = new User { Name = "High Approver", Email = "high@test.com", PasswordHash = "hash", DepartmentId = deptB.Id, Level = 3 };
            var higherApprover = new User { Name = "Higher Approver", Email = "higher@test.com", PasswordHash = "hash", DepartmentId = deptB.Id, Level = 4 };
            _context.Users.AddRange(uploader, highApprover, higherApprover);
            await _context.SaveChangesAsync();

            var mockFile = CreateMockFile("test.png", "image/png", 500);

            var request = new UploadDocumentRequestDto
            {
                Title = "Cross-Dept Document",
                TargetDepartmentId = deptB.Id,
                File = mockFile.Object
            };

            var result = await _documentService.UploadDocument(request, uploader.Id);

            Assert.That(result.CurrentApproverUserId, Is.EqualTo(highApprover.Id));
            Assert.That(result.DocumentStatus, Is.EqualTo("PendingApproval"));
        }

        [Test]
        public async Task GetDocumentFileAsync_PhysicalFileNotFound_ThrowsEntityNotFoundException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(uploader);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Document",
                Description = "Description",
                CreatedByUserId = uploader.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                TargetDepartmentId = dept.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var version = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = 1,
                OriginalFileName = "test.pdf",
                StoredFileName = "nonexistent_stored_file.pdf",
                MimeType = "application/pdf",
                IsCurrentVersion = true,
                UploadedByUserId = uploader.Id
            };
            _context.DocumentVersions.Add(version);
            await _context.SaveChangesAsync();

            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.GetDocumentFileAsync(document.Id, uploader.Id));
            Assert.That(ex.Message, Is.EqualTo($"Physical file for version {version.VersionNumber} of document {document.Id} was not found on the server."));
        }

        [Test]
        public void ReUploadDocumentVersionAsync_FileIsNull_ThrowsArgumentException()
        {
            var request = new ReUploadDocumentRequestDto { File = null! };
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _documentService.ReUploadDocumentVersionAsync(1, request, 1));
        }

        [Test]
        public void ReUploadDocumentVersionAsync_FileLengthZero_ThrowsArgumentException()
        {
            var mockFile = CreateMockFile("empty.pdf", "application/pdf", 0);
            var request = new ReUploadDocumentRequestDto { File = mockFile.Object };
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _documentService.ReUploadDocumentVersionAsync(1, request, 1));
        }

        [Test]
        public async Task ReUploadDocumentVersionAsync_UploaderNull_ThrowsEntityNotFoundException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Rejected Document",
                CreatedByUserId = 9999,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.Rejected
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var mockFile = CreateMockFile("new.pdf", "application/pdf", 100);
            var request = new ReUploadDocumentRequestDto { File = mockFile.Object };

            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.ReUploadDocumentVersionAsync(document.Id, request, 9999));
            Assert.That(ex.Message, Is.EqualTo("Uploader user not found."));
        }

        [Test]
        public async Task GetDocumentFileAsync_WithValidVersionId_ReturnsFileSuccessfully()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(uploader);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Document",
                Description = "Description",
                CreatedByUserId = uploader.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                TargetDepartmentId = dept.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var version = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = 1,
                OriginalFileName = "version1.pdf",
                StoredFileName = "stored_version1.pdf",
                MimeType = "application/pdf",
                IsCurrentVersion = true,
                UploadedByUserId = uploader.Id
            };
            _context.DocumentVersions.Add(version);
            await _context.SaveChangesAsync();

            Directory.CreateDirectory(_testUploadsFolder);
            string physicalPath = Path.Combine(_testUploadsFolder, "stored_version1.pdf");
            await File.WriteAllTextAsync(physicalPath, "dummy content");

            var result = await _documentService.GetDocumentFileAsync(document.Id, uploader.Id, versionId: version.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.VersionNumber, Is.EqualTo(1));
            Assert.That(result.OriginalFileName, Is.EqualTo("version1.pdf"));
            Assert.That(result.FilePath, Is.EqualTo(physicalPath));
        }
    }
}