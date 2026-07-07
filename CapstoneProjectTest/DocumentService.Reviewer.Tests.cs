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
        public async Task GetDocumentsPendingApprovalByUserAsync_WithSearch_ReturnsMatchingDocuments()
        {
            var dept1 = new Department { Name = "HR" };
            var dept2 = new Department { Name = "IT" };
            _context.Departments.AddRange(dept1, dept2);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept1.Id };
            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = dept1.Id };
            _context.Users.AddRange(uploader, approver);
            await _context.SaveChangesAsync();

            var doc1 = new Document
            {
                Title = "Financial Report",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept1.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CurrentApproverUserId = approver.Id
            };
            var doc2 = new Document
            {
                Title = "Vacation Request",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept2.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CurrentApproverUserId = approver.Id
            };
            _context.Documents.AddRange(doc1, doc2);
            await _context.SaveChangesAsync();

            // Search by Title
            var resTitle = await _documentService.GetDocumentsPendingApprovalByUserAsync(approver.Id, pageNumber: 1, pageSize: 10, search: "financial");
            Assert.That(resTitle.TotalCount, Is.EqualTo(1));
            Assert.That(resTitle.Items[0].Title, Is.EqualTo("Financial Report"));

            // Search by CreatedByUser Name
            var resUser = await _documentService.GetDocumentsPendingApprovalByUserAsync(approver.Id, pageNumber: 1, pageSize: 10, search: "uploader");
            Assert.That(resUser.TotalCount, Is.EqualTo(2));

            // Search by TargetDepartment Name
            var resDept = await _documentService.GetDocumentsPendingApprovalByUserAsync(approver.Id, pageNumber: 1, pageSize: 10, search: "it");
            Assert.That(resDept.TotalCount, Is.EqualTo(1));
            Assert.That(resDept.Items[0].Title, Is.EqualTo("Vacation Request"));

            // Search with no matches
            var resEmpty = await _documentService.GetDocumentsPendingApprovalByUserAsync(approver.Id, pageNumber: 1, pageSize: 10, search: "nomatch");
            Assert.That(resEmpty.TotalCount, Is.EqualTo(0));
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
            Assert.That(action!.Action, Is.EqualTo(ApprovalActionType.Transfered));
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
                TargetUserId = targetUser.Id
            };

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _documentService.ApproveDocumentAsync(document.Id, request, approver.Id));
            Assert.That(ex.Message, Is.EqualTo("ApproveAndTransfer requires the target user to belong to a different department. Use ApproveAndForward to escalate within the same department."));
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

        [Test]
        public void ApproveDocumentAsync_DocumentNotFound_ThrowsEntityNotFoundException()
        {
            var request = new ApproveDocumentRequestDto
            {
                Action = ApproveDocumentAction.ApproveEntirely
            };

            Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.ApproveDocumentAsync(9999, request, 1));
        }

        [Test]
        public async Task ApproveDocumentAsync_CurrentApproverNotFoundInDatabase_ThrowsEntityNotFoundException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.Add(uploader);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Doc",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CurrentApproverUserId = 9999
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var request = new ApproveDocumentRequestDto
            {
                Action = ApproveDocumentAction.ApproveEntirely
            };

            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.ApproveDocumentAsync(document.Id, request, 9999));
            Assert.That(ex.Message, Is.EqualTo("Approver user not found."));
        }

        [Test]
        public async Task ApproveDocumentAsync_CurrentApproverHasNoManager_ThrowsInvalidOperationException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = dept.Id, ManagerId = null };
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
                Action = ApproveDocumentAction.ApproveAndForward,
                Comments = "Forward"
            };

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _documentService.ApproveDocumentAsync(document.Id, request, approver.Id));
            Assert.That(ex.Message, Is.EqualTo("Cannot forward: the current approver has no manager in this department."));
        }

        [Test]
        public async Task ApproveDocumentAsync_CurrentApproversManagerNotFoundInDatabase_ThrowsEntityNotFoundException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var approver = new User { Name = "Approver", Email = "approver@test.com", PasswordHash = "hash", DepartmentId = dept.Id, ManagerId = 8888 };
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
                Action = ApproveDocumentAction.ApproveAndForward,
                Comments = "Forward"
            };

            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.ApproveDocumentAsync(document.Id, request, approver.Id));
            Assert.That(ex.Message, Is.EqualTo("Manager user not found."));
        }

        [Test]
        public async Task ApproveDocumentAsync_ApproveAndTransfer_TargetUserIdNull_ThrowsArgumentException()
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
                Action = ApproveDocumentAction.ApproveAndTransfer,
                TargetUserId = null
            };

            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _documentService.ApproveDocumentAsync(document.Id, request, approver.Id));
            Assert.That(ex.Message, Is.EqualTo("TargetUserId is required when action is ApproveAndTransfer."));
        }

        [Test]
        public async Task ApproveDocumentAsync_ApproveAndTransfer_TargetUserDoesNotExist_ThrowsEntityNotFoundException()
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
                Action = ApproveDocumentAction.ApproveAndTransfer,
                TargetUserId = 9999
            };

            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.ApproveDocumentAsync(document.Id, request, approver.Id));
            Assert.That(ex.Message, Is.EqualTo("Target user with ID 9999 was not found."));
        }

        [Test]
        public async Task ApproveDocumentAsync_UnknownAction_ThrowsArgumentException()
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
                Action = (ApproveDocumentAction)999,
                Comments = "Unknown Action"
            };

            var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
                await _documentService.ApproveDocumentAsync(document.Id, request, approver.Id));
            Assert.That(ex.Message, Is.EqualTo("Unknown approval action '999'."));
        }

        [Test]
        public void TransferDocumentAsync_DocumentNotFound_ThrowsEntityNotFoundException()
        {
            var request = new TransferDocumentRequestDto
            {
                TargetUserId = 1,
                Comments = "Transfer"
            };

            Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.TransferDocumentAsync(9999, request, 1));
        }

        [Test]
        public async Task TransferDocumentAsync_UserNotCurrentApprover_ThrowsUnauthorizedAccessException()
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

            var request = new TransferDocumentRequestDto
            {
                TargetUserId = otherUser.Id,
                Comments = "Transfer"
            };

            var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _documentService.TransferDocumentAsync(document.Id, request, otherUser.Id));
            Assert.That(ex.Message, Is.EqualTo("You are not authorised to transfer this document."));
        }

        [Test]
        public async Task TransferDocumentAsync_DocumentNotPendingApproval_ThrowsInvalidOperationException()
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
                DocumentStatus = DocumentStatus.Approved,
                CurrentApproverUserId = approver.Id
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var request = new TransferDocumentRequestDto
            {
                TargetUserId = otherUser.Id,
                Comments = "Transfer"
            };

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _documentService.TransferDocumentAsync(document.Id, request, approver.Id));
            Assert.That(ex.Message, Is.EqualTo("Only documents with status 'PendingApproval' can be transferred. Current status is 'Approved'."));
        }

        [Test]
        public async Task TransferDocumentAsync_TargetUserDoesNotExist_ThrowsEntityNotFoundException()
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

            var request = new TransferDocumentRequestDto
            {
                TargetUserId = 9999,
                Comments = "Transfer"
            };

            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.TransferDocumentAsync(document.Id, request, approver.Id));
            Assert.That(ex.Message, Is.EqualTo("Target user with ID 9999 was not found."));
        }

        [Test]
        public async Task TransferDocumentAsync_CurrentApproverNotFoundInDatabase_ThrowsEntityNotFoundException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var uploader = new User { Name = "Uploader", Email = "uploader@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            var targetUser = new User { Name = "Target", Email = "target@test.com", PasswordHash = "hash", DepartmentId = dept.Id };
            _context.Users.AddRange(uploader, targetUser);
            await _context.SaveChangesAsync();

            var document = new Document
            {
                Title = "Test Doc",
                CreatedByUserId = uploader.Id,
                TargetDepartmentId = dept.Id,
                DocumentStatus = DocumentStatus.PendingApproval,
                CurrentApproverUserId = 8888
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
                Comments = "Transfer"
            };

            var ex = Assert.ThrowsAsync<EntityNotFoundException>(async () =>
                await _documentService.TransferDocumentAsync(document.Id, request, 8888));
            Assert.That(ex.Message, Is.EqualTo("Current approver user not found."));
        }
    }
}