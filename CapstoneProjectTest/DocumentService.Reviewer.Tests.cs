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
        public async Task GetDocumentsPendingApprovalByUserAsync_ValidUser_ReturnsPendingDocuments()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(uploader, approver);
            await _context.SaveChangesAsync();

            _context.Documents.Add(new Document { Title = "Pending 1", CreatedByUserId = uploader.Id, TargetDepartmentId = dept.Id, DocumentStatus = DocumentStatus.PendingApproval, CurrentApproverUserId = approver.Id, CreatedAt = DateTime.UtcNow.AddMinutes(-5) });
            _context.Documents.Add(new Document { Title = "Pending 2", CreatedByUserId = uploader.Id, TargetDepartmentId = dept.Id, DocumentStatus = DocumentStatus.PendingApproval, CurrentApproverUserId = approver.Id, CreatedAt = DateTime.UtcNow.AddMinutes(-2) });
            _context.Documents.Add(new Document { Title = "Approved", CreatedByUserId = uploader.Id, TargetDepartmentId = dept.Id, DocumentStatus = DocumentStatus.Approved, CurrentApproverUserId = approver.Id });
            _context.Documents.Add(new Document { Title = "Other Approver", CreatedByUserId = uploader.Id, TargetDepartmentId = dept.Id, DocumentStatus = DocumentStatus.PendingApproval, CurrentApproverUserId = 999 });
            await _context.SaveChangesAsync();

            var result = await _documentService.GetDocumentsPendingApprovalByUserAsync(approver.Id, pageNumber: 1, pageSize: 10);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.Items.Count, Is.EqualTo(2));
            Assert.That(result.Items.First().Title, Is.EqualTo("Pending 2"));
            Assert.That(result.Items.Last().Title, Is.EqualTo("Pending 1"));
        }

        [Test]
        public void GetDocumentsPendingApprovalByUserAsync_UserNotFound_ThrowsEntityNotFoundException()
        {
            Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.GetDocumentsPendingApprovalByUserAsync(999, 1, 10));
        }

        [Test]
        public async Task RejectDocumentAsync_ValidRequest_RejectsDocumentAndCreatesAuditAndApprovalAction()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(uploader, approver);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Doc",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CurrentApproverUserId = approver.Id
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

            await _documentService.RejectDocumentAsync(document.Id, approver.Id, "Missing signature");

            var updatedDoc = await _context.Documents.FindAsync(document.Id);
            Assert.That(updatedDoc, Is.Not.Null);
            Assert.That(updatedDoc!.DocumentStatus, Is.EqualTo(DocumentStatus.Rejected));
            Assert.That(updatedDoc.CurrentApproverUserId, Is.Null);

            var approvalAction = await _context.ApprovalActions.FirstOrDefaultAsync(aa => aa.DocumentId == document.Id);
            Assert.That(approvalAction, Is.Not.Null);
            Assert.That(approvalAction!.Action, Is.EqualTo(ApprovalActionType.Rejected));
            Assert.That(approvalAction.Comments, Is.EqualTo("Missing signature"));
            Assert.That(approvalAction.ApproverUserId, Is.EqualTo(approver.Id));

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(al => al.DocumentId == document.Id);
            Assert.That(auditLog, Is.Not.Null);
            Assert.That(auditLog!.Action, Is.EqualTo(AuditAction.DocumentRejected));
            Assert.That(auditLog.Details, Contains.Substring("Missing signature"));
        }

        [Test]
        public void RejectDocumentAsync_DocumentNotFound_ThrowsEntityNotFoundException()
        {
            Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.RejectDocumentAsync(999, 1, "Reason"));
        }

        [Test]
        public async Task RejectDocumentAsync_UserNotCurrentApprover_ThrowsUnauthorizedAccessException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var otherUser = new User { Name = "Other", Email = "other@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(uploader, approver, otherUser);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Doc",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CurrentApproverUserId = approver.Id
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _documentService.RejectDocumentAsync(document.Id, otherUser.Id, "Reason"));
        }

        [Test]
        public async Task RejectDocumentAsync_DocumentNotPendingApproval_ThrowsInvalidOperationException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(uploader, approver);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Doc",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.Approved,
                CurrentApproverUserId = approver.Id
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _documentService.RejectDocumentAsync(document.Id, approver.Id, "Reason"));
        }

        [Test]
        public async Task ApproveDocumentAsync_ApproveEntirely_ValidRequest_ApprovesEntirely()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(uploader, approver);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Doc",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CurrentApproverUserId = approver.Id
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

            var request = new ApproveDocumentRequestDto
            {
                Action = ApproveDocumentAction.ApproveEntirely,
                Comments = "Approved entirely"
            };

            var result = await _documentService.ApproveDocumentAsync(document.Id, request, approver.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.DocumentStatus, Is.EqualTo("Approved"));
            Assert.That(result.ActionTaken, Is.EqualTo("ApproveEntirely"));

            var updatedDoc = await _context.Documents.FindAsync(document.Id);
            Assert.That(updatedDoc, Is.Not.Null);
            Assert.That(updatedDoc!.DocumentStatus, Is.EqualTo(DocumentStatus.Approved));
            Assert.That(updatedDoc.CurrentApproverUserId, Is.Null);

            var action = await _context.ApprovalActions.FirstOrDefaultAsync(aa => aa.DocumentId == document.Id);
            Assert.That(action, Is.Not.Null);
            Assert.That(action!.Action, Is.EqualTo(ApprovalActionType.Approved));
            Assert.That(action.Comments, Is.EqualTo("Approved entirely"));

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(al => al.DocumentId == document.Id && al.Action == AuditAction.DocumentApproved);
            Assert.That(auditLog, Is.Not.Null);
        }

        [Test]
        public async Task ApproveDocumentAsync_ApproveAndForward_ValidRequest_EscalatesToManager()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var manager = new User { Name = "Manager", Email = "manager@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(manager);
            await _context.SaveChangesAsync();

            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = dept.Id, ManagerId = manager.Id };
            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(approver, uploader);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Doc",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CurrentApproverUserId = approver.Id
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

            var request = new ApproveDocumentRequestDto
            {
                Action = ApproveDocumentAction.ApproveAndForward,
                Comments = "Escalating to manager"
            };

            var result = await _documentService.ApproveDocumentAsync(document.Id, request, approver.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.NewApproverUserId, Is.EqualTo(manager.Id));
            Assert.That(result.NewApproverName, Is.EqualTo("Manager"));
            Assert.That(result.ActionTaken, Is.EqualTo("ApproveAndForward"));

            var updatedDoc = await _context.Documents.FindAsync(document.Id);
            Assert.That(updatedDoc, Is.Not.Null);
            Assert.That(updatedDoc!.CurrentApproverUserId, Is.EqualTo(manager.Id));

            var action = await _context.ApprovalActions.FirstOrDefaultAsync(aa => aa.DocumentId == document.Id);
            Assert.That(action, Is.Not.Null);
            Assert.That(action!.Action, Is.EqualTo(ApprovalActionType.Forwarded));

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(al => al.DocumentId == document.Id && al.Action == AuditAction.DocumentForwarded);
            Assert.That(auditLog, Is.Not.Null);
        }

        [Test]
        public async Task ApproveDocumentAsync_ApproveAndTransfer_ValidRequest_TransfersCrossDept()
        {
            var deptA = new Department { Name = "HR" };
            var deptB = new Department { Name = "Finance" };
            _context.Departments.AddRange(deptA, deptB);
            await _context.SaveChangesAsync();

            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = deptA.Id };
            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = deptA.Id };
            var targetUser = new User { Name = "Target", Email = "target@test.com", PasswordHash = "hash", DepartmentId = deptB.Id };
            _context.Users.AddRange(approver, uploader, targetUser);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Doc",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = deptA.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CurrentApproverUserId = approver.Id
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

            var request = new ApproveDocumentRequestDto
            {
                Action = ApproveDocumentAction.ApproveAndTransfer,
                TargetUserId = targetUser.Id,
                Comments = "Transfer to Finance"
            };

            var result = await _documentService.ApproveDocumentAsync(document.Id, request, approver.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.NewApproverUserId, Is.EqualTo(targetUser.Id));
            Assert.That(result.NewApproverDepartmentName, Is.EqualTo("Finance"));
            Assert.That(result.ForwardedToDepartmentId, Is.EqualTo(deptB.Id));
            Assert.That(result.ActionTaken, Is.EqualTo("ApproveAndTransfer"));

            var updatedDoc = await _context.Documents.FindAsync(document.Id);
            Assert.That(updatedDoc, Is.Not.Null);
            Assert.That(updatedDoc!.CurrentApproverUserId, Is.EqualTo(targetUser.Id));

            var action = await _context.ApprovalActions.FirstOrDefaultAsync(aa => aa.DocumentId == document.Id);
            Assert.That(action, Is.Not.Null);
            Assert.That(action!.Action, Is.EqualTo(ApprovalActionType.Forwarded));
            Assert.That(action.ForwardedToDepartmentId, Is.EqualTo(deptB.Id));

            var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(al => al.DocumentId == document.Id && al.Action == AuditAction.DocumentForwarded);
            Assert.That(auditLog, Is.Not.Null);
        }

        [Test]
        public async Task ApproveDocumentAsync_ApproveAndTransfer_SameDept_ThrowsInvalidOperationException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var targetUser = new User { Name = "Target Same Dept", Email = "targetsame@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(approver, uploader, targetUser);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Doc",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CurrentApproverUserId = approver.Id
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var request = new ApproveDocumentRequestDto
            {
                Action = ApproveDocumentAction.ApproveAndTransfer,
                TargetUserId = targetUser.Id
            };

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _documentService.ApproveDocumentAsync(document.Id, request, approver.Id));
        }

        [Test]
        public async Task ApproveDocumentAsync_UserNotCurrentApprover_ThrowsUnauthorizedAccessException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var otherUser = new User { Name = "Other", Email = "other@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(uploader, approver, otherUser);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Doc",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CurrentApproverUserId = approver.Id
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var request = new ApproveDocumentRequestDto
            {
                Action = ApproveDocumentAction.ApproveEntirely
            };

            Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _documentService.ApproveDocumentAsync(document.Id, request, otherUser.Id));
        }

        [Test]
        public async Task ApproveDocumentAsync_DocumentNotPendingApproval_ThrowsInvalidOperationException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(uploader, approver);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Doc",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.Approved,
                CurrentApproverUserId = approver.Id
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var request = new ApproveDocumentRequestDto
            {
                Action = ApproveDocumentAction.ApproveEntirely
            };

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _documentService.ApproveDocumentAsync(document.Id, request, approver.Id));
        }

        [Test]
        public async Task TransferDocumentAsync_ValidRequest_SameDept_TransfersCorrectly()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var targetUser = new User { Name = "Target", Email = "target@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(approver, targetUser, uploader);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Doc",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CurrentApproverUserId = approver.Id
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

            var request = new TransferDocumentRequestDto
            {
                TargetUserId = targetUser.Id,
                Comments = "Transferring in same dept"
            };

            var result = await _documentService.TransferDocumentAsync(document.Id, request, approver.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.NewApproverUserId, Is.EqualTo(targetUser.Id));
            Assert.That(result.ActionTaken, Is.EqualTo("TransferWithoutApproval"));
            Assert.That(result.ForwardedToDepartmentId, Is.Null);

            var updatedDoc = await _context.Documents.FindAsync(document.Id);
            Assert.That(updatedDoc, Is.Not.Null);
            Assert.That(updatedDoc!.CurrentApproverUserId, Is.EqualTo(targetUser.Id));

            var action = await _context.ApprovalActions.FirstOrDefaultAsync(aa => aa.DocumentId == document.Id);
            Assert.That(action, Is.Not.Null);
            Assert.That(action!.Action, Is.EqualTo(ApprovalActionType.Forwarded));
            Assert.That(action.ForwardedToDepartmentId, Is.Null);
        }

        [Test]
        public async Task TransferDocumentAsync_ValidRequest_CrossDept_TransfersCorrectly()
        {
            var deptA = new Department { Name = "HR" };
            var deptB = new Department { Name = "Finance" };
            _context.Departments.AddRange(deptA, deptB);
            await _context.SaveChangesAsync();

            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = deptA.Id };
            var targetUser = new User { Name = "Target", Email = "target@test.com", PasswordHash = "hash", DepartmentId = deptB.Id };
            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = deptA.Id };
            _context.Users.AddRange(approver, targetUser, uploader);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Doc",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = deptA.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CurrentApproverUserId = approver.Id
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

            var request = new TransferDocumentRequestDto
            {
                TargetUserId = targetUser.Id,
                Comments = "Transferring to Finance"
            };

            var result = await _documentService.TransferDocumentAsync(document.Id, request, approver.Id);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.NewApproverUserId, Is.EqualTo(targetUser.Id));
            Assert.That(result.ForwardedToDepartmentId, Is.EqualTo(deptB.Id));
            Assert.That(result.ActionTaken, Is.EqualTo("TransferWithoutApproval"));

            var updatedDoc = await _context.Documents.FindAsync(document.Id);
            Assert.That(updatedDoc, Is.Not.Null);
            Assert.That(updatedDoc!.CurrentApproverUserId, Is.EqualTo(targetUser.Id));

            var action = await _context.ApprovalActions.FirstOrDefaultAsync(aa => aa.DocumentId == document.Id);
            Assert.That(action, Is.Not.Null);
            Assert.That(action!.Action, Is.EqualTo(ApprovalActionType.Forwarded));
            Assert.That(action.ForwardedToDepartmentId, Is.EqualTo(deptB.Id));
        }

        [Test]
        public async Task TransferDocumentAsync_TransferToSelf_ThrowsArgumentException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(approver);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Doc",
                CreatedByUserId = 1,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CurrentApproverUserId = approver.Id
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var request = new TransferDocumentRequestDto
            {
                TargetUserId = approver.Id
            };

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _documentService.TransferDocumentAsync(document.Id, request, approver.Id));
        }
    }
}