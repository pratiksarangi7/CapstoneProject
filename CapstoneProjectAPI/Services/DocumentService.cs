using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Exceptions;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace CapstoneProjectAPI.Services
{
    public class DocumentService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/jpeg",
            "image/png"
        };

        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

        public DocumentService(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<UploadDocumentResponseDto> UploadDocument(UploadDocumentRequestDto request, int uploaderUserId)
        {
            // ── Validate file ──────────────────────────────────────────────────
            if (request.File == null || request.File.Length == 0)
            {
                throw new ArgumentException("File is required.");
            }

            if (request.File.Length > MaxFileSizeBytes)
            {
                throw new ArgumentException("File size exceeds the maximum allowed size of 5 MB.");
            }

            // Validate MIME type from the file content, not extension
            string mimeType = request.File.ContentType;
            if (!AllowedMimeTypes.Contains(mimeType))
            {
                throw new ArgumentException($"Invalid file type '{mimeType}'. Allowed types: PDF, JPEG, PNG.");
            }

            // ── Validate uploader exists ───────────────────────────────────────
            var uploader = await _context.Users.FindAsync(uploaderUserId)
                ?? throw new EntityNotFoundException("Uploader user not found.");

            // ── Validate target department exists ──────────────────────────────
            var targetDeptExists = await _context.Departments.AnyAsync(d => d.Id == request.TargetDepartmentId);
            if (!targetDeptExists)
            {
                throw new EntityNotFoundException("Target department not found.");
            }

            // ── Determine the first approver ───────────────────────────────────
            int? approverUserId = await DetermineFirstApprover(uploader, request.TargetDepartmentId);

            // ── Save the file to disk ──────────────────────────────────────────
            string uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads");
            Directory.CreateDirectory(uploadsFolder);

            string storedFileName = $"{Guid.NewGuid()}{Path.GetExtension(request.File.FileName)}";
            string filePath = Path.Combine(uploadsFolder, storedFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }

            // ── Create Document record ─────────────────────────────────────────
            var document = new Document
            {
                Title = request.Title,
                Description = request.Description,
                CreatedByUserId = uploaderUserId,
                DocumentStatus = approverUserId.HasValue ? DocumentStatus.PendingApproval : DocumentStatus.Approved,
                CurrentApproverUserId = approverUserId,
                TargetDepartmentId = request.TargetDepartmentId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            // ── Create DocumentVersion record ──────────────────────────────────
            var documentVersion = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = 1,
                OriginalFileName = request.File.FileName,
                StoredFileName = storedFileName,
                FileSize = request.File.Length,
                MimeType = mimeType,
                IsCurrentVersion = true,
                UploadedByUserId = uploaderUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.DocumentVersions.Add(documentVersion);
            await _context.SaveChangesAsync();

            // ── Build response ─────────────────────────────────────────────────
            string? approverName = null;
            if (approverUserId.HasValue)
            {
                var approver = await _context.Users.FindAsync(approverUserId.Value);
                approverName = approver?.Name;
            }

            return new UploadDocumentResponseDto
            {
                DocumentId = document.Id,
                Title = document.Title,
                Description = document.Description,
                DocumentStatus = document.DocumentStatus.ToString(),
                TargetDepartmentId = document.TargetDepartmentId,
                CurrentApproverUserId = approverUserId,
                CurrentApproverName = approverName,
                VersionNumber = documentVersion.VersionNumber,
                OriginalFileName = documentVersion.OriginalFileName,
                FileSize = documentVersion.FileSize,
                MimeType = documentVersion.MimeType,
                CreatedAt = document.CreatedAt
            };
        }

        /// <summary>
        /// Withdraws a document that is still pending approval.
        /// Only the user who originally uploaded the document can withdraw it.
        /// Deletes the document record, all versions, approval actions, audit logs, and physical files.
        /// </summary>
        public async Task WithdrawDocumentAsync(int documentId, int requestingUserId)
        {
            // ── Load document with all related data ────────────────────────────
            var document = await _context.Documents
                .Include(d => d.Versions)
                    .ThenInclude(v => v.AuditLogs)
                .Include(d => d.AuditLogs)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null)
                throw new EntityNotFoundException($"Document with ID {documentId} was not found.");

            // ── Ownership check ────────────────────────────────────────────────
            if (document.CreatedByUserId != requestingUserId)
                throw new UnauthorizedAccessException("You are not authorised to withdraw this document.");

            // ── Status check ───────────────────────────────────────────────────
            if (document.DocumentStatus != DocumentStatus.PendingApproval)
                throw new InvalidOperationException(
                    $"Document cannot be withdrawn because its status is '{document.DocumentStatus}'. " +
                    "Only documents with status 'PendingApproval' can be withdrawn.");

            // ── Delete audit logs linked to versions (FK is NoAction – must go first) ──
            foreach (var version in document.Versions)
            {
                if (version.AuditLogs.Any())
                    _context.AuditLogs.RemoveRange(version.AuditLogs);
            }

            // ── Delete document-level audit logs ───────────────────────────────
            if (document.AuditLogs.Any())
                _context.AuditLogs.RemoveRange(document.AuditLogs);

            // ── Delete physical files from disk ────────────────────────────────
            string uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads");
            foreach (var version in document.Versions)
            {
                string filePath = Path.Combine(uploadsFolder, version.StoredFileName);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }

            // ── Remove document (EF cascades: DocumentVersion, ApprovalAction) ─
            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Determines the first approver for a document.
        /// - Same department: uploader's direct manager.
        /// - Different department: user at the same Level in the target department
        ///   with the fewest pending approvals (least workload). Falls back to the
        ///   nearest higher level if no exact match exists.
        /// </summary>
        private async Task<int?> DetermineFirstApprover(User uploader, int targetDepartmentId)
        {
            // ── Same department → direct manager ───────────────────────────────
            if (uploader.DepartmentId == targetDepartmentId)
            {
                // If uploader has no manager, document is auto-approved (top of chain)
                return uploader.ManagerId;
            }

            // ── Different department → least-workload peer at same level ───────
            // Try exact level match first
            var approver = await _context.Users
                .Where(u => u.DepartmentId == targetDepartmentId && u.Level == uploader.Level)
                .OrderBy(u => _context.Documents.Count(d =>
                    d.CurrentApproverUserId == u.Id &&
                    d.DocumentStatus == DocumentStatus.PendingApproval))
                .FirstOrDefaultAsync();

            if (approver != null)
            {
                return approver.Id;
            }

            // Fallback: nearest higher level in target department
            approver = await _context.Users
                .Where(u => u.DepartmentId == targetDepartmentId && u.Level > uploader.Level)
                .OrderBy(u => u.Level)
                .ThenBy(u => _context.Documents.Count(d =>
                    d.CurrentApproverUserId == u.Id &&
                    d.DocumentStatus == DocumentStatus.PendingApproval))
                .FirstOrDefaultAsync();

            if (approver != null)
            {
                return approver.Id;
            }

            // No eligible approver found in target department
            throw new InvalidOperationException(
                "No eligible approver found in the target department.");
        }

        public async Task<IEnumerable<UserDocumentResponseDto>> GetDocumentsUploadedByUserAsync(int userId)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                throw new EntityNotFoundException("User not found.");
            }

            var documents = await _context.Documents
                .Include(d => d.TargetDepartment)
                .Include(d => d.CreatedByUser)
                .Include(d => d.CurrentApprover)
                    .ThenInclude(u => u != null ? u.Department : null)
                .Include(d => d.Versions)
                    .ThenInclude(v => v.UploadedByUser)
                .Where(d => d.CreatedByUserId == userId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            return documents.Select(d => new UserDocumentResponseDto
            {
                Id = d.Id,
                Title = d.Title,
                Description = d.Description,
                DocumentStatus = d.DocumentStatus.ToString(),
                CreatedAt = d.CreatedAt,
                TargetDepartmentId = d.TargetDepartmentId,
                TargetDepartmentName = d.TargetDepartment.Name,
                CurrentApproverUserId = d.CurrentApproverUserId,
                CurrentApproverName = d.CurrentApprover?.Name,
                CurrentApproverEmail = d.CurrentApprover?.Email,
                CurrentApproverDepartmentName = d.CurrentApprover?.Department?.Name,
                CreatedByUserId = d.CreatedByUserId,
                CreatedByUserName = d.CreatedByUser.Name,
                CreatedByUserEmail = d.CreatedByUser.Email,
                Versions = d.Versions.Select(v => new DocumentVersionResponseDto
                {
                    Id = v.Id,
                    DocumentId = v.DocumentId,
                    VersionNumber = v.VersionNumber,
                    OriginalFileName = v.OriginalFileName,
                    StoredFileName = v.StoredFileName,
                    FileSize = v.FileSize,
                    MimeType = v.MimeType,
                    IsCurrentVersion = v.IsCurrentVersion,
                    UploadedByUserId = v.UploadedByUserId,
                    UploadedByUserName = v.UploadedByUser.Name,
                    CreatedAt = v.CreatedAt
                })
                .OrderByDescending(v => v.VersionNumber)
                .ToList()
            }).ToList();
        }

        public async Task<IEnumerable<UserDocumentResponseDto>> GetDocumentsPendingApprovalByUserAsync(int userId)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                throw new EntityNotFoundException("User not found.");
            }

            var documents = await _context.Documents
                .Include(d => d.TargetDepartment)
                .Include(d => d.CreatedByUser)
                .Include(d => d.CurrentApprover)
                    .ThenInclude(u => u != null ? u.Department : null)
                .Include(d => d.Versions)
                    .ThenInclude(v => v.UploadedByUser)
                .Include(d => d.Versions)
                    .ThenInclude(v => v.ApprovalActions)
                        .ThenInclude(aa => aa.ApproverUser)
                .Where(d => d.CurrentApproverUserId == userId && d.DocumentStatus == DocumentStatus.PendingApproval)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            return documents.Select(d => new UserDocumentResponseDto
            {
                Id = d.Id,
                Title = d.Title,
                Description = d.Description,
                DocumentStatus = d.DocumentStatus.ToString(),
                CreatedAt = d.CreatedAt,
                TargetDepartmentId = d.TargetDepartmentId,
                TargetDepartmentName = d.TargetDepartment.Name,
                CurrentApproverUserId = d.CurrentApproverUserId,
                CurrentApproverName = d.CurrentApprover?.Name,
                CurrentApproverEmail = d.CurrentApprover?.Email,
                CurrentApproverDepartmentName = d.CurrentApprover?.Department?.Name,
                CreatedByUserId = d.CreatedByUserId,
                CreatedByUserName = d.CreatedByUser.Name,
                CreatedByUserEmail = d.CreatedByUser.Email,
                Versions = d.Versions.Select(v =>
                {
                    var action = v.ApprovalActions.OrderByDescending(aa => aa.CreatedAt).FirstOrDefault();
                    return new DocumentVersionResponseDto
                    {
                        Id = v.Id,
                        DocumentId = v.DocumentId,
                        VersionNumber = v.VersionNumber,
                        OriginalFileName = v.OriginalFileName,
                        StoredFileName = v.StoredFileName,
                        FileSize = v.FileSize,
                        MimeType = v.MimeType,
                        IsCurrentVersion = v.IsCurrentVersion,
                        UploadedByUserId = v.UploadedByUserId,
                        UploadedByUserName = v.UploadedByUser.Name,
                        CreatedAt = v.CreatedAt,
                        ApprovalAction = action != null ? new ApprovalActionResponseDto
                        {
                            Id = action.Id,
                            ApproverUserId = action.ApproverUserId,
                            ApproverUserName = action.ApproverUser.Name,
                            Action = action.Action.ToString(),
                            Comments = action.Comments,
                            CreatedAt = action.CreatedAt
                        } : null
                    };
                })
                .OrderByDescending(v => v.VersionNumber)
                .ToList()
            }).ToList();
        }

        /// <summary>
        /// Rejects a document that is pending approval by the given approver.
        /// - Validates that the document exists, is PendingApproval, and the requesting user is the current approver.
        /// - Creates an ApprovalAction record with the rejection reason.
        /// - Sets DocumentStatus to Rejected and clears CurrentApproverUserId.
        /// - Writes an AuditLog entry.
        /// </summary>
        public async Task RejectDocumentAsync(int documentId, int approverUserId, string reason)
        {
            // ── Load document with current version ────────────────────────────
            var document = await _context.Documents
                .Include(d => d.Versions)
                .FirstOrDefaultAsync(d => d.Id == documentId)
                ?? throw new EntityNotFoundException($"Document with ID {documentId} was not found.");

            // ── Authorization check ────────────────────────────────────────────
            if (document.CurrentApproverUserId != approverUserId)
                throw new UnauthorizedAccessException("You are not authorised to reject this document.");

            // ── Status check ───────────────────────────────────────────────────
            if (document.DocumentStatus != DocumentStatus.PendingApproval)
                throw new InvalidOperationException(
                    $"Document cannot be rejected because its status is '{document.DocumentStatus}'. " +
                    "Only documents with status 'PendingApproval' can be rejected.");

            // ── Identify current version ───────────────────────────────────────
            var currentVersion = document.Versions.FirstOrDefault(v => v.IsCurrentVersion)
                ?? document.Versions.OrderByDescending(v => v.VersionNumber).First();

            // ── Create ApprovalAction (rejection) ──────────────────────────────
            var approvalAction = new ApprovalAction
            {
                DocumentId = document.Id,
                DocumentVersionId = currentVersion.Id,
                ApproverUserId = approverUserId,
                Action = ApprovalActionType.Rejected,
                Comments = reason,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.ApprovalActions.Add(approvalAction);

            // ── Update document status ─────────────────────────────────────────
            document.DocumentStatus = DocumentStatus.Rejected;
            document.CurrentApproverUserId = null;

            // ── Write audit log ────────────────────────────────────────────────
            var auditLog = new AuditLog
            {
                PerformedByUserId = approverUserId,
                DocumentId = document.Id,
                DocumentVersionId = currentVersion.Id,
                Action = AuditAction.DocumentRejected,
                Details = $"Document rejected. Reason: {reason}",
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Uploads a new version for a document that has been rejected.
        /// - Only the original creator can re-upload.
        /// - Document must have status Rejected.
        /// - Marks all previous versions as non-current.
        /// - Increments version number and saves the new file.
        /// - Re-determines the approver and sets status back to PendingApproval.
        /// - Writes an AuditLog entry.
        /// </summary>
        public async Task<UploadDocumentResponseDto> ReUploadDocumentVersionAsync(
            int documentId, ReUploadDocumentRequestDto request, int uploaderUserId)
        {
            // ── Validate file ──────────────────────────────────────────────────
            if (request.File == null || request.File.Length == 0)
                throw new ArgumentException("File is required.");

            if (request.File.Length > MaxFileSizeBytes)
                throw new ArgumentException("File size exceeds the maximum allowed size of 5 MB.");

            string mimeType = request.File.ContentType;
            if (!AllowedMimeTypes.Contains(mimeType))
                throw new ArgumentException($"Invalid file type '{mimeType}'. Allowed types: PDF, JPEG, PNG.");

            // ── Load document with its versions ───────────────────────────────
            var document = await _context.Documents
                .Include(d => d.Versions)
                .FirstOrDefaultAsync(d => d.Id == documentId)
                ?? throw new EntityNotFoundException($"Document with ID {documentId} was not found.");

            // ── Ownership check ────────────────────────────────────────────────
            if (document.CreatedByUserId != uploaderUserId)
                throw new UnauthorizedAccessException("You are not authorised to re-upload this document.");

            // ── Status check ───────────────────────────────────────────────────
            if (document.DocumentStatus != DocumentStatus.Rejected)
                throw new InvalidOperationException(
                    $"A new version can only be uploaded for rejected documents. " +
                    $"Current status is '{document.DocumentStatus}'.");

            // ── Validate uploader exists ───────────────────────────────────────
            var uploader = await _context.Users.FindAsync(uploaderUserId)
                ?? throw new EntityNotFoundException("Uploader user not found.");

            // ── Mark all previous versions as non-current ──────────────────────
            foreach (var v in document.Versions)
                v.IsCurrentVersion = false;

            // ── Save the new file to disk ──────────────────────────────────────
            string uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads");
            Directory.CreateDirectory(uploadsFolder);

            string storedFileName = $"{Guid.NewGuid()}{Path.GetExtension(request.File.FileName)}";
            string filePath = Path.Combine(uploadsFolder, storedFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }

            // ── Determine new version number ───────────────────────────────────
            int nextVersionNumber = document.Versions.Any()
                ? document.Versions.Max(v => v.VersionNumber) + 1
                : 1;

            // ── Re-determine approver ──────────────────────────────────────────
            int? approverUserId = await DetermineFirstApprover(uploader, document.TargetDepartmentId);

            // ── Create new DocumentVersion record ──────────────────────────────
            var newVersion = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = nextVersionNumber,
                OriginalFileName = request.File.FileName,
                StoredFileName = storedFileName,
                FileSize = request.File.Length,
                MimeType = mimeType,
                IsCurrentVersion = true,
                UploadedByUserId = uploaderUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.DocumentVersions.Add(newVersion);

            // ── Update document status ─────────────────────────────────────────
            document.DocumentStatus = approverUserId.HasValue
                ? DocumentStatus.PendingApproval
                : DocumentStatus.Approved;
            document.CurrentApproverUserId = approverUserId;

            // ── Write audit log ────────────────────────────────────────────────
            var auditLog = new AuditLog
            {
                PerformedByUserId = uploaderUserId,
                DocumentId = document.Id,
                Action = AuditAction.NewVersionUploaded,
                Details = $"New version {nextVersionNumber} uploaded for rejected document.",
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();

            // ── Build response ─────────────────────────────────────────────────
            string? approverName = null;
            if (approverUserId.HasValue)
            {
                var approver = await _context.Users.FindAsync(approverUserId.Value);
                approverName = approver?.Name;
            }

            return new UploadDocumentResponseDto
            {
                DocumentId = document.Id,
                Title = document.Title,
                Description = document.Description,
                DocumentStatus = document.DocumentStatus.ToString(),
                TargetDepartmentId = document.TargetDepartmentId,
                CurrentApproverUserId = approverUserId,
                CurrentApproverName = approverName,
                VersionNumber = newVersion.VersionNumber,
                OriginalFileName = newVersion.OriginalFileName,
                FileSize = newVersion.FileSize,
                MimeType = newVersion.MimeType,
                CreatedAt = document.CreatedAt
            };
        }

        // ── Approval ───────────────────────────────────────────────────────────────
        /// <summary>
        /// Processes an approval decision on a pending document.
        /// Three actions are supported:
        ///   ApproveEntirely        – final approval, no further review.
        ///   ApproveAndForward      – approve and escalate to current approver's manager (same dept).
        ///   ApproveAndTransfer     – approve and hand off to a specific user in a different department.
        /// </summary>
        public async Task<ApproveDocumentResponseDto> ApproveDocumentAsync(
            int documentId, ApproveDocumentRequestDto request, int approverUserId)
        {
            // ── Load document ──────────────────────────────────────────────────
            var document = await _context.Documents
                .Include(d => d.Versions)
                .Include(d => d.TargetDepartment)
                .FirstOrDefaultAsync(d => d.Id == documentId)
                ?? throw new EntityNotFoundException($"Document with ID {documentId} was not found.");

            if (document.CurrentApproverUserId != approverUserId)
                throw new UnauthorizedAccessException("You are not authorised to approve this document.");

            if (document.DocumentStatus != DocumentStatus.PendingApproval)
                throw new InvalidOperationException(
                    $"Document cannot be approved because its status is '{document.DocumentStatus}'. " +
                    "Only documents with status 'PendingApproval' can be approved.");

            var currentApprover = await _context.Users
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.Id == approverUserId)
                ?? throw new EntityNotFoundException("Approver user not found.");

            var currentVersion = document.Versions.FirstOrDefault(v => v.IsCurrentVersion)
                ?? document.Versions.OrderByDescending(v => v.VersionNumber).First();

            var actedAt = DateTimeOffset.UtcNow;
            ApproveDocumentResponseDto response;

            switch (request.Action)
            {
                case ApproveDocumentAction.ApproveEntirely:
                {
                    document.DocumentStatus = DocumentStatus.Approved;
                    document.CurrentApproverUserId = null;

                    _context.ApprovalActions.Add(new ApprovalAction
                    {
                        DocumentId = document.Id,
                        DocumentVersionId = currentVersion.Id,
                        ApproverUserId = approverUserId,
                        Action = ApprovalActionType.Approved,
                        Comments = request.Comments,
                        CreatedAt = actedAt
                    });

                    _context.AuditLogs.Add(new AuditLog
                    {
                        PerformedByUserId = approverUserId,
                        DocumentId = document.Id,
                        DocumentVersionId = currentVersion.Id,
                        Action = AuditAction.DocumentApproved,
                        Details = "Document approved entirely — no further review required.",
                        CreatedAt = actedAt
                    });

                    response = new ApproveDocumentResponseDto
                    {
                        DocumentId = document.Id,
                        Title = document.Title,
                        DocumentStatus = document.DocumentStatus.ToString(),
                        ActionTaken = nameof(ApproveDocumentAction.ApproveEntirely),
                        ApproverComments = request.Comments,
                        ActedAt = actedAt
                    };
                    break;
                }

                case ApproveDocumentAction.ApproveAndForward:
                {
                    if (!currentApprover.ManagerId.HasValue)
                        throw new InvalidOperationException(
                            "Cannot forward: the current approver has no manager in this department.");

                    var manager = await _context.Users
                        .Include(u => u.Department)
                        .FirstOrDefaultAsync(u => u.Id == currentApprover.ManagerId.Value)
                        ?? throw new EntityNotFoundException("Manager user not found.");

                    document.CurrentApproverUserId = manager.Id;

                    _context.ApprovalActions.Add(new ApprovalAction
                    {
                        DocumentId = document.Id,
                        DocumentVersionId = currentVersion.Id,
                        ApproverUserId = approverUserId,
                        Action = ApprovalActionType.Forwarded,
                        Comments = request.Comments,
                        CreatedAt = actedAt
                    });

                    _context.AuditLogs.Add(new AuditLog
                    {
                        PerformedByUserId = approverUserId,
                        DocumentId = document.Id,
                        DocumentVersionId = currentVersion.Id,
                        Action = AuditAction.DocumentForwarded,
                        Details = $"Document forwarded to manager '{manager.Name}' (same department).",
                        CreatedAt = actedAt
                    });

                    response = new ApproveDocumentResponseDto
                    {
                        DocumentId = document.Id,
                        Title = document.Title,
                        DocumentStatus = document.DocumentStatus.ToString(),
                        ActionTaken = nameof(ApproveDocumentAction.ApproveAndForward),
                        NewApproverUserId = manager.Id,
                        NewApproverName = manager.Name,
                        NewApproverDepartmentName = manager.Department?.Name,
                        ApproverComments = request.Comments,
                        ActedAt = actedAt
                    };
                    break;
                }

                case ApproveDocumentAction.ApproveAndTransfer:
                {
                    if (!request.TargetUserId.HasValue)
                        throw new ArgumentException(
                            "TargetUserId is required when action is ApproveAndTransfer.");

                    var targetUser = await _context.Users
                        .Include(u => u.Department)
                        .FirstOrDefaultAsync(u => u.Id == request.TargetUserId.Value)
                        ?? throw new EntityNotFoundException(
                            $"Target user with ID {request.TargetUserId.Value} was not found.");

                    if (targetUser.DepartmentId == currentApprover.DepartmentId)
                        throw new InvalidOperationException(
                            "ApproveAndTransfer requires the target user to belong to a different department. " +
                            "Use ApproveAndForward to escalate within the same department.");

                    document.CurrentApproverUserId = targetUser.Id;
                    document.DocumentStatus = DocumentStatus.PendingApproval;

                    _context.ApprovalActions.Add(new ApprovalAction
                    {
                        DocumentId = document.Id,
                        DocumentVersionId = currentVersion.Id,
                        ApproverUserId = approverUserId,
                        Action = ApprovalActionType.Forwarded,
                        ForwardedToDepartmentId = targetUser.DepartmentId,
                        Comments = request.Comments,
                        CreatedAt = actedAt
                    });

                    _context.AuditLogs.Add(new AuditLog
                    {
                        PerformedByUserId = approverUserId,
                        DocumentId = document.Id,
                        DocumentVersionId = currentVersion.Id,
                        Action = AuditAction.DocumentForwarded,
                        Details = $"Document transferred to '{targetUser.Name}' in department '{targetUser.Department?.Name}'.",
                        CreatedAt = actedAt
                    });

                    response = new ApproveDocumentResponseDto
                    {
                        DocumentId = document.Id,
                        Title = document.Title,
                        DocumentStatus = document.DocumentStatus.ToString(),
                        ActionTaken = nameof(ApproveDocumentAction.ApproveAndTransfer),
                        NewApproverUserId = targetUser.Id,
                        NewApproverName = targetUser.Name,
                        NewApproverDepartmentName = targetUser.Department?.Name,
                        ForwardedToDepartmentId = targetUser.DepartmentId,
                        ForwardedToDepartmentName = targetUser.Department?.Name,
                        ApproverComments = request.Comments,
                        ActedAt = actedAt
                    };
                    break;
                }

                default:
                    throw new ArgumentException($"Unknown approval action '{request.Action}'.");
            }

            await _context.SaveChangesAsync();
            return response;
        }

        /// <summary>
        /// Transfers a pending document directly to another user without approving it.
        /// Useful when the document was sent to the wrong department or is outside
        /// the current approver's authority.
        /// - Does NOT change DocumentStatus (stays PendingApproval).
        /// - Target user can be in the same or a different department.
        /// - Creates an ApprovalAction (Forwarded) and an AuditLog entry.
        /// </summary>
        public async Task<ApproveDocumentResponseDto> TransferDocumentAsync(
            int documentId, TransferDocumentRequestDto request, int currentApproverUserId)
        {
            // ── Load document ──────────────────────────────────────────────────
            var document = await _context.Documents
                .Include(d => d.TargetDepartment)
                .Include(d => d.Versions)
                .FirstOrDefaultAsync(d => d.Id == documentId)
                ?? throw new EntityNotFoundException($"Document with ID {documentId} was not found.");

            // ── Authorization check ────────────────────────────────────────────
            if (document.CurrentApproverUserId != currentApproverUserId)
                throw new UnauthorizedAccessException("You are not authorised to transfer this document.");

            // ── Status check ───────────────────────────────────────────────────
            if (document.DocumentStatus != DocumentStatus.PendingApproval)
                throw new InvalidOperationException(
                    $"Only documents with status 'PendingApproval' can be transferred. " +
                    $"Current status is '{document.DocumentStatus}'.");

            // ── Validate target user ───────────────────────────────────────────
            if (request.TargetUserId == currentApproverUserId)
                throw new ArgumentException("Cannot transfer the document to yourself.");

            var targetUser = await _context.Users
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.Id == request.TargetUserId)
                ?? throw new EntityNotFoundException(
                    $"Target user with ID {request.TargetUserId} was not found.");

            // ── Resolve current approver for department comparison ─────────────
            var currentApprover = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == currentApproverUserId)
                ?? throw new EntityNotFoundException("Current approver user not found.");

            // ── Identify current version ───────────────────────────────────────
            var currentVersion = document.Versions.FirstOrDefault(v => v.IsCurrentVersion)
                ?? document.Versions.OrderByDescending(v => v.VersionNumber).First();

            var actedAt = DateTimeOffset.UtcNow;

            // ── Update approver — status stays PendingApproval ─────────────────
            document.CurrentApproverUserId = targetUser.Id;

            // ── Create ApprovalAction (Forwarded) ──────────────────────────────
            _context.ApprovalActions.Add(new ApprovalAction
            {
                DocumentId = document.Id,
                DocumentVersionId = currentVersion.Id,
                ApproverUserId = currentApproverUserId,
                Action = ApprovalActionType.Forwarded,
                // Only set if cross-department
                ForwardedToDepartmentId = targetUser.DepartmentId != currentApprover.DepartmentId
                    ? targetUser.DepartmentId
                    : null,
                Comments = request.Comments,
                CreatedAt = actedAt
            });

            // ── Write audit log ────────────────────────────────────────────────
            _context.AuditLogs.Add(new AuditLog
            {
                PerformedByUserId = currentApproverUserId,
                DocumentId = document.Id,
                DocumentVersionId = currentVersion.Id,
                Action = AuditAction.DocumentForwarded,
                Details = targetUser.DepartmentId != currentApprover.DepartmentId
                    ? $"Document transferred to '{targetUser.Name}' in department '{targetUser.Department?.Name}' (without approval)."
                    : $"Document transferred to '{targetUser.Name}' in the same department (without approval).",
                CreatedAt = actedAt
            });

            await _context.SaveChangesAsync();

            return new ApproveDocumentResponseDto
            {
                DocumentId = document.Id,
                Title = document.Title,
                DocumentStatus = document.DocumentStatus.ToString(),
                ActionTaken = "TransferWithoutApproval",
                NewApproverUserId = targetUser.Id,
                NewApproverName = targetUser.Name,
                NewApproverDepartmentName = targetUser.Department?.Name,
                ForwardedToDepartmentId = targetUser.DepartmentId != currentApprover.DepartmentId
                    ? targetUser.DepartmentId
                    : null,
                ForwardedToDepartmentName = targetUser.DepartmentId != currentApprover.DepartmentId
                    ? targetUser.Department?.Name
                    : null,
                ApproverComments = request.Comments,
                ActedAt = actedAt
            };
        }
    }
}
