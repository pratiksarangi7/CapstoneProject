using CapstoneProjectAPI.Interfaces;
using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Exceptions;
using CapstoneProjectAPI.Misc;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapstoneProjectAPI.Services
{
    public class DocumentService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DocumentService> _logger;
        private readonly IBlobStorageService _blobStorageService;
        private readonly IGeminiService _geminiService;
        private readonly IEmailService _emailService;

        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/jpeg",
            "image/png"
        };

        private const long MaxFileSizeBytes = 5 * 1024 * 1024;

        public DocumentService(
            AppDbContext context,
            IWebHostEnvironment environment,
            ILogger<DocumentService> logger,
            IBlobStorageService blobStorageService,
            IGeminiService geminiService,
            IEmailService emailService)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
            _blobStorageService = blobStorageService;
            _geminiService = geminiService;
            _emailService = emailService;
        }

        public async Task<UploadDocumentResponseDto> UploadDocument(UploadDocumentRequestDto request, int uploaderUserId)
        {
            if (request.File == null || request.File.Length == 0)
            {
                _logger.LogWarning("Document upload failed: File is missing or empty.");
                throw new ArgumentException("File is required.");
            }

            if (request.File.Length > MaxFileSizeBytes)
            {
                _logger.LogWarning("Document upload failed: File size {FileSize} bytes exceeds limit of 5 MB.", request.File.Length);
                throw new ArgumentException("File size exceeds the maximum allowed size of 5 MB.");
            }

            string mimeType = request.File.ContentType;
            if (!AllowedMimeTypes.Contains(mimeType))
            {
                _logger.LogWarning("Document upload failed: MimeType '{MimeType}' is invalid.", mimeType);
                throw new ArgumentException($"Invalid file type '{mimeType}'. Allowed types: PDF, JPEG, PNG.");
            }

            var uploader = await _context.Users.FindAsync(uploaderUserId);
            if (uploader == null)
            {
                _logger.LogWarning("Document upload failed: Uploader user with ID {UserId} was not found.", uploaderUserId);
                throw new EntityNotFoundException("Uploader user not found.");
            }

            var targetDeptExists = await _context.Departments.AnyAsync(d => d.Id == request.TargetDepartmentId);
            if (!targetDeptExists)
            {
                _logger.LogWarning("Document upload failed: Target department with ID {DeptId} not found.", request.TargetDepartmentId);
                throw new EntityNotFoundException("Target department not found.");
            }

            bool isDuplicateTitle = await _context.Documents.AnyAsync(d =>
                d.CreatedByUserId == uploaderUserId &&
                d.TargetDepartmentId == request.TargetDepartmentId &&
                d.Title == request.Title &&
                (d.DocumentStatus == DocumentStatus.PendingApproval ||
                 d.DocumentStatus == DocumentStatus.Approved));

            if (isDuplicateTitle)
            {
                _logger.LogWarning(
                    "Document upload failed: User {UserId} already has an active document titled '{Title}' for department {DeptId}.",
                    uploaderUserId, request.Title, request.TargetDepartmentId);
                throw new InvalidOperationException(
                    "A document with the same title is already active (PendingApproval or Approved) " +
                    "for this department. Please withdraw it or use a different title.");
            }

            string contentHash = await ComputeSha256HashAsync(request.File);

            bool isDuplicateContent = await _context.DocumentVersions.AnyAsync(dv =>
                dv.UploadedByUserId == uploaderUserId &&
                dv.ContentHash == contentHash &&
                dv.Document.TargetDepartmentId == request.TargetDepartmentId &&
                (dv.Document.DocumentStatus == DocumentStatus.PendingApproval ||
                 dv.Document.DocumentStatus == DocumentStatus.Approved));

            if (isDuplicateContent)
            {
                _logger.LogWarning(
                    "Document upload failed: User {UserId} already has an active document with identical file content (hash: {Hash}).",
                    uploaderUserId, contentHash);
                throw new InvalidOperationException(
                    "An identical file has already been uploaded and is currently active. " +
                    "Please upload a different document.");
            }

            int? approverUserId;
            try
            {
                approverUserId = await DetermineFirstApprover(uploader, request.TargetDepartmentId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Document upload failed: DetermineFirstApprover threw an exception.");
                throw;
            }

            string storedFileName = $"{Guid.NewGuid()}{Path.GetExtension(request.File.FileName)}";
            await _blobStorageService.UploadFileAsync(request.File.OpenReadStream(), storedFileName, mimeType);

            string? aiSummary = null;
            try
            {
                using var summaryStream = request.File.OpenReadStream();
                aiSummary = await _geminiService.GenerateSummaryAsync(summaryStream, mimeType, request.File.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "AI summary generation failed for document '{Title}' (file: '{FileName}'). Proceeding without summary.",
                    request.Title, request.File.FileName);
            }
            // ----------------------------------

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

            var documentVersion = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = 1,
                OriginalFileName = request.File.FileName,
                StoredFileName = storedFileName,
                FileSize = request.File.Length,
                MimeType = mimeType,
                ContentHash = contentHash,
                AiSummary = aiSummary,
                IsCurrentVersion = true,
                UploadedByUserId = uploaderUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.DocumentVersions.Add(documentVersion);
            await _context.SaveChangesAsync();
            _context.AuditLogs.Add(new AuditLog()
            {
                PerformedByUserId = uploaderUserId,
                Action = AuditAction.DocumentUploaded,
                DocumentId = document.Id,
                DocumentVersionId = documentVersion.Id,
                Details = $"User #{uploader.Id} uploaded new document to department #{request.TargetDepartmentId}"
            });
            await _context.SaveChangesAsync();

            string? approverName = null;
            if (approverUserId.HasValue)
            {
                var approver = await _context.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.Id == approverUserId.Value);
                approverName = approver?.Name;

                // Fire-and-forget: notify the approver that a document awaits their review.
                if (approver != null)
                {
                    var dept = await _context.Departments.FindAsync(request.TargetDepartmentId);
                    _ = Task.Run(() => _emailService.SendDocumentUploadedToApproverAsync(
                        approverEmail:        approver.Email,
                        approverName:         approver.Name,
                        uploaderName:         uploader.Name,
                        documentTitle:        document.Title,
                        documentDescription:  document.Description,
                        targetDepartmentName: dept?.Name ?? request.TargetDepartmentId.ToString(),
                        fileName:             request.File.FileName,
                        fileSize:             request.File.Length,
                        mimeType:             mimeType,
                        documentId:           document.Id,
                        uploadedAt:           document.CreatedAt));
                }
            }

            _logger.LogInformation("Document {DocumentId} ('{Title}') successfully uploaded by user {UserId}. Status: {Status}, Next Approver: {ApproverId}.",
                document.Id, document.Title, uploaderUserId, document.DocumentStatus, document.CurrentApproverUserId);

            return new UploadDocumentResponseDto
            {
                DocumentId = document.Id,
                Title = document.Title,
                Description = document.Description,
                DocumentStatus = document.DocumentStatus.ToString(),
                TargetDepartmentId = document.TargetDepartmentId,
                CurrentApproverUserId = document.CurrentApproverUserId,
                CurrentApproverName = approverName,
                VersionNumber = documentVersion.VersionNumber,
                OriginalFileName = documentVersion.OriginalFileName,
                FileSize = documentVersion.FileSize,
                MimeType = documentVersion.MimeType,
                CreatedAt = document.CreatedAt
            };
        }

        public async Task WithdrawDocumentAsync(int documentId, int requestingUserId)
        {
            var document = await _context.Documents
        .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null)
            {
                _logger.LogWarning("Withdraw failed: Document with ID {DocumentId} not found.", documentId);
                throw new EntityNotFoundException($"Document with ID {documentId} was not found.");
            }

            if (document.CreatedByUserId != requestingUserId)
            {
                _logger.LogWarning("Withdraw failed: User {UserId} is not authorized to withdraw document {DocumentId}.", requestingUserId, documentId);
                throw new UnauthorizedAccessException("You are not authorised to withdraw this document.");
            }

            if (document.DocumentStatus != DocumentStatus.PendingApproval)
            {
                _logger.LogWarning("Withdraw failed: Document {DocumentId} is in status '{Status}'. Only PendingApproval documents can be withdrawn.", documentId, document.DocumentStatus);
                throw new InvalidOperationException(
                    $"Document cannot be withdrawn because its status is '{document.DocumentStatus}'. " +
                    "Only documents with status 'PendingApproval' can be withdrawn.");
            }

            document.DocumentStatus = DocumentStatus.DocumentWithdrawn;
            document.CurrentApproverUserId = null;

            _context.AuditLogs.Add(new AuditLog()
            {
                PerformedByUserId = requestingUserId,
                Action = AuditAction.DocumentWithdrawn,
                DocumentId = documentId,
                Details = $"Document {documentId} that was uploaded by {requestingUserId} was withdrawn"
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Document {DocumentId} successfully marked as withdrawn by user {UserId}.", documentId, requestingUserId);
        }

        private async Task<int?> DetermineFirstApprover(User uploader, int targetDepartmentId)
        {
            if (uploader.DepartmentId == targetDepartmentId)
                return uploader.ManagerId;

            var approver = await _context.Users
                .Where(u => u.DepartmentId == targetDepartmentId && u.Level == uploader.Level)
                .OrderBy(u => _context.Documents.Count(d =>
                    d.CurrentApproverUserId == u.Id &&
                    d.DocumentStatus == DocumentStatus.PendingApproval))
                .FirstOrDefaultAsync();

            if (approver != null)
                return approver.Id;

            approver = await _context.Users
                .Where(u => u.DepartmentId == targetDepartmentId && u.Level > uploader.Level)
                .OrderBy(u => u.Level)
                .ThenBy(u => _context.Documents.Count(d =>
                    d.CurrentApproverUserId == u.Id &&
                    d.DocumentStatus == DocumentStatus.PendingApproval))
                .FirstOrDefaultAsync();

            if (approver != null)
                return approver.Id;

            throw new InvalidOperationException("No eligible approver found in the target department.");
        }

        private static ApprovalActionResponseDto? MapApprovalAction(ApprovalAction? action)
        {
            if (action == null)
                return null;

            return new ApprovalActionResponseDto
            {
                Id = action.Id,
                ApproverUserId = action.ApproverUserId,
                ApproverUserName = action.ApproverUser?.Name ?? string.Empty,
                Action = action.Action.ToString(),
                Comments = action.Comments,
                CreatedAt = action.CreatedAt
            };
        }

        private static DocumentVersionResponseDto MapDocumentVersion(DocumentVersion version)
        {
            var latestAction = version.ApprovalActions
                ?.OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();

            return new DocumentVersionResponseDto
            {
                Id = version.Id,
                DocumentId = version.DocumentId,
                VersionNumber = version.VersionNumber,
                OriginalFileName = version.OriginalFileName,
                StoredFileName = version.StoredFileName,
                FileSize = version.FileSize,
                MimeType = version.MimeType,
                AiSummary = version.AiSummary,
                IsCurrentVersion = version.IsCurrentVersion,
                UploadedByUserId = version.UploadedByUserId,
                UploadedByUserName = version.UploadedByUser?.Name ?? string.Empty,
                CreatedAt = version.CreatedAt,
                ApprovalAction = MapApprovalAction(latestAction)
            };
        }

        public static UserDocumentResponseDto MapUserDocument(Document document)
        {
            return new UserDocumentResponseDto
            {
                Id = document.Id,
                Title = document.Title,
                Description = document.Description,
                DocumentStatus = document.DocumentStatus.ToString(),
                CreatedAt = document.CreatedAt,
                TargetDepartmentId = document.TargetDepartmentId,
                TargetDepartmentName = document.TargetDepartment?.Name ?? string.Empty,
                CurrentApproverUserId = document.CurrentApproverUserId,
                CurrentApproverName = document.CurrentApprover?.Name,
                CurrentApproverEmail = document.CurrentApprover?.Email,
                CurrentApproverDepartmentName = document.CurrentApprover?.Department?.Name,
                CreatedByUserId = document.CreatedByUserId,
                CreatedByUserName = document.CreatedByUser?.Name ?? string.Empty,
                CreatedByUserEmail = document.CreatedByUser?.Email ?? string.Empty,
                Versions = document.Versions
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(MapDocumentVersion)
                    .ToList()
            };
        }

        public async Task<PagedResult<UserDocumentResponseDto>> GetDocumentsUploadedByUserAsync(
    int userId,
    int pageNumber = 1,
    int pageSize = 10,
    string search = "",
    DocumentStatus? status = null)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
                throw new EntityNotFoundException("User not found.");

            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var query = _context.Documents
                .Include(d => d.TargetDepartment)
                .Include(d => d.CreatedByUser)
                .Include(d => d.CurrentApprover)
                    .ThenInclude(u => u!.Department)
                .Include(d => d.Versions)
                    .ThenInclude(v => v.UploadedByUser)
                .Include(d => d.Versions)
                    .ThenInclude(v => v.ApprovalActions)
                        .ThenInclude(aa => aa.ApproverUser)
                .AsSplitQuery()
                .Where(d => d.CreatedByUserId == userId);

            if (status.HasValue)
            {
                query = query.Where(d => d.DocumentStatus == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string lowerSearch = search.ToLower();

                query = query.Where(d => d.Title.ToLower().Contains(lowerSearch) ||
                                     d.CreatedByUser.Name.ToLower().Contains(lowerSearch) ||
                                     d.TargetDepartment.Name.ToLower().Contains(lowerSearch));
            }

            query = query.OrderByDescending(d => d.CreatedAt);

            int totalCount = await query.CountAsync();

            var documents = await query.Skip((pageNumber - 1) * pageSize)
                                       .Take(pageSize)
                                       .ToListAsync();

            return new PagedResult<UserDocumentResponseDto>()
            {
                Items = documents.Select(DocumentService.MapUserDocument).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<PagedResult<UserDocumentResponseDto>> GetDocumentsPendingApprovalByUserAsync(
    int userId,
    int pageNumber = 1,
    int pageSize = 10,
    string search = "")
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
                throw new EntityNotFoundException("User not found.");

            var query = _context.Documents
                .Include(d => d.TargetDepartment)
                .Include(d => d.CreatedByUser)
                .Include(d => d.CurrentApprover)
                    .ThenInclude(u => u!.Department)
                .Include(d => d.Versions)
                    .ThenInclude(v => v.UploadedByUser)
                .Include(d => d.Versions)
                    .ThenInclude(v => v.ApprovalActions)
                        .ThenInclude(aa => aa.ApproverUser)
                        .AsSplitQuery()
                .Where(d => d.CurrentApproverUserId == userId && d.DocumentStatus == DocumentStatus.PendingApproval);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string lowerSearch = search.ToLower();

                query = query.Where(d => d.Title.ToLower().Contains(lowerSearch) ||
                                         d.CreatedByUser.Name.ToLower().Contains(lowerSearch) ||
                                         d.TargetDepartment.Name.ToLower().Contains(lowerSearch));
            }

            query = query.OrderByDescending(d => d.CreatedAt);

            int totalCount = await query.CountAsync();

            var documents = await query.Skip((pageNumber - 1) * pageSize)
                                       .Take(pageSize)
                                       .ToListAsync();

            return new PagedResult<UserDocumentResponseDto>()
            {
                Items = documents.Select(MapUserDocument).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task RejectDocumentAsync(int documentId, int approverUserId, string reason)
        {
            var document = await _context.Documents
                .Include(d => d.Versions)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null)
            {
                _logger.LogWarning("Rejection failed: Document with ID {DocumentId} not found.", documentId);
                throw new EntityNotFoundException($"Document with ID {documentId} was not found.");
            }

            if (document.CurrentApproverUserId != approverUserId)
            {
                _logger.LogWarning("Rejection failed: User {UserId} is not the current approver for document {DocumentId}.", approverUserId, documentId);
                throw new UnauthorizedAccessException("You are not authorised to reject this document.");
            }

            if (document.DocumentStatus != DocumentStatus.PendingApproval)
            {
                _logger.LogWarning("Rejection failed: Document {DocumentId} is in status '{Status}'. Only PendingApproval documents can be rejected.", documentId, document.DocumentStatus);
                throw new InvalidOperationException(
                    $"Document cannot be rejected because its status is '{document.DocumentStatus}'. " +
                    "Only documents with status 'PendingApproval' can be rejected.");
            }

            var currentVersion = document.Versions.FirstOrDefault(v => v.IsCurrentVersion)
                ?? document.Versions.OrderByDescending(v => v.VersionNumber).First();

            _context.ApprovalActions.Add(new ApprovalAction
            {
                DocumentId = document.Id,
                DocumentVersionId = currentVersion.Id,
                ApproverUserId = approverUserId,
                Action = ApprovalActionType.Rejected,
                Comments = reason,
                CreatedAt = DateTimeOffset.UtcNow
            });

            document.DocumentStatus = DocumentStatus.Rejected;
            document.CurrentApproverUserId = null;

            _context.AuditLogs.Add(new AuditLog
            {
                PerformedByUserId = approverUserId,
                DocumentId = document.Id,
                DocumentVersionId = currentVersion.Id,
                Action = AuditAction.DocumentRejected,
                Details = $"Document rejected. Reason: {reason}",
                CreatedAt = DateTimeOffset.UtcNow
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Document {DocumentId} was successfully rejected by approver {ApproverUserId}. Reason: {Reason}", documentId, approverUserId, reason);
        }

        public async Task<UploadDocumentResponseDto> ReUploadDocumentVersionAsync(
            int documentId, ReUploadDocumentRequestDto request, int uploaderUserId)
        {
            if (request.File == null || request.File.Length == 0)
            {
                _logger.LogWarning("Re-upload failed: File is missing or empty.");
                throw new ArgumentException("File is required.");
            }

            if (request.File.Length > MaxFileSizeBytes)
            {
                _logger.LogWarning("Re-upload failed: File size {FileSize} bytes exceeds limit of 5 MB.", request.File.Length);
                throw new ArgumentException("File size exceeds the maximum allowed size of 5 MB.");
            }

            string mimeType = request.File.ContentType;
            if (!AllowedMimeTypes.Contains(mimeType))
            {
                _logger.LogWarning("Re-upload failed: MimeType '{MimeType}' is invalid.", mimeType);
                throw new ArgumentException($"Invalid file type '{mimeType}'. Allowed types: PDF, JPEG, PNG.");
            }

            var document = await _context.Documents
                .Include(d => d.Versions)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null)
            {
                _logger.LogWarning("Re-upload failed: Document with ID {DocumentId} not found.", documentId);
                throw new EntityNotFoundException($"Document with ID {documentId} was not found.");
            }

            if (document.CreatedByUserId != uploaderUserId)
            {
                _logger.LogWarning("Re-upload failed: User {UserId} is not authorized to re-upload for document {DocumentId}.", uploaderUserId, documentId);
                throw new UnauthorizedAccessException("You are not authorised to re-upload this document.");
            }

            if (document.DocumentStatus != DocumentStatus.Rejected)
            {
                _logger.LogWarning("Re-upload failed: Document {DocumentId} is in status '{Status}'. Re-uploads only allowed for Rejected status.", documentId, document.DocumentStatus);
                throw new InvalidOperationException(
                    $"A new version can only be uploaded for rejected documents. " +
                    $"Current status is '{document.DocumentStatus}'.");
            }

            var uploader = await _context.Users.FindAsync(uploaderUserId);
            if (uploader == null)
            {
                _logger.LogWarning("Re-upload failed: Uploader user with ID {UserId} was not found.", uploaderUserId);
                throw new EntityNotFoundException("Uploader user not found.");
            }

            string contentHash = await ComputeSha256HashAsync(request.File);

            bool isDuplicateContent = await _context.DocumentVersions.AnyAsync(dv =>
                dv.UploadedByUserId == uploaderUserId &&
                dv.ContentHash == contentHash &&
                dv.Document.TargetDepartmentId == document.TargetDepartmentId &&
                (dv.Document.DocumentStatus == DocumentStatus.PendingApproval ||
                 dv.Document.DocumentStatus == DocumentStatus.Approved ||
                 dv.Document.DocumentStatus== DocumentStatus.Rejected));

            if (isDuplicateContent)
            {
                _logger.LogWarning(
                    "Re-upload failed: User {UserId} already has an active document with identical file content (hash: {Hash}).",
                    uploaderUserId, contentHash);
                throw new InvalidOperationException(
                    "An identical file has already been uploaded and is currently active. " +
                    "Please upload a different document.");
            }

            var currentRejectedVersion = document.Versions.FirstOrDefault(v => v.IsCurrentVersion);
            if (currentRejectedVersion != null && currentRejectedVersion.ContentHash == contentHash)
            {
                _logger.LogWarning(
                    "Re-upload failed: User {UserId} attempted to upload the exact same file that was rejected.",
                    uploaderUserId);
                throw new InvalidOperationException(
                    "This file is identical to the version that was just rejected. Please upload a revised document.");
            }
            foreach (var v in document.Versions)
                v.IsCurrentVersion = false;

            string storedFileName = $"{Guid.NewGuid()}{Path.GetExtension(request.File.FileName)}";
            await _blobStorageService.UploadFileAsync(request.File.OpenReadStream(), storedFileName, mimeType);

            // --- AI Summary ---
            string? aiSummary = null;
            try
            {
                using var summaryStream = request.File.OpenReadStream();
                aiSummary = await _geminiService.GenerateSummaryAsync(summaryStream, mimeType, request.File.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "AI summary generation failed during re-upload for document {DocumentId} (file: '{FileName}'). Proceeding without summary.",
                    documentId, request.File.FileName);
            }

            int nextVersionNumber = document.Versions.Any()
                ? document.Versions.Max(v => v.VersionNumber) + 1
                : 1;

            int? approverUserId = await DetermineFirstApprover(uploader, document.TargetDepartmentId);

            var newVersion = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = nextVersionNumber,
                OriginalFileName = request.File.FileName,
                StoredFileName = storedFileName,
                FileSize = request.File.Length,
                MimeType = mimeType,
                AiSummary = aiSummary,
                IsCurrentVersion = true,
                UploadedByUserId = uploaderUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.DocumentVersions.Add(newVersion);

            document.DocumentStatus = approverUserId.HasValue
                ? DocumentStatus.PendingApproval
                : DocumentStatus.Approved;
            document.CurrentApproverUserId = approverUserId;

            _context.AuditLogs.Add(new AuditLog
            {
                PerformedByUserId = uploaderUserId,
                DocumentId = document.Id,
                Action = AuditAction.NewVersionUploaded,
                Details = $"New version {nextVersionNumber} uploaded for rejected document.",
                CreatedAt = DateTimeOffset.UtcNow
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully re-uploaded document version {Version} for document {DocumentId} by user {UserId}. Next Approver: {ApproverId}.",
                nextVersionNumber, documentId, uploaderUserId, document.CurrentApproverUserId);

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
                CurrentApproverUserId = document.CurrentApproverUserId,
                CurrentApproverName = approverName,
                VersionNumber = newVersion.VersionNumber,
                OriginalFileName = newVersion.OriginalFileName,
                FileSize = newVersion.FileSize,
                MimeType = newVersion.MimeType,
                CreatedAt = document.CreatedAt
            };
        }

        public async Task<ApproveDocumentResponseDto> ApproveDocumentAsync(
            int documentId, ApproveDocumentRequestDto request, int approverUserId)
        {
            var document = await _context.Documents
                .Include(d => d.Versions)
                .Include(d => d.TargetDepartment)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null)
            {
                _logger.LogWarning("Approval failed: Document with ID {DocumentId} not found.", documentId);
                throw new EntityNotFoundException($"Document with ID {documentId} was not found.");
            }

            if (document.CurrentApproverUserId != approverUserId)
            {
                _logger.LogWarning("Approval failed: User {UserId} is not the current approver for document {DocumentId}.", approverUserId, documentId);
                throw new UnauthorizedAccessException("You are not authorised to approve this document.");
            }

            if (document.DocumentStatus != DocumentStatus.PendingApproval)
            {
                _logger.LogWarning("Approval failed: Document {DocumentId} is in status '{Status}'. Only PendingApproval documents can be approved.", documentId, document.DocumentStatus);
                throw new InvalidOperationException(
                    $"Document cannot be approved because its status is '{document.DocumentStatus}'. " +
                    "Only documents with status 'PendingApproval' can be approved.");
            }

            var currentApprover = await _context.Users
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.Id == approverUserId);

            if (currentApprover == null)
            {
                _logger.LogWarning("Approval failed: Approver user with ID {UserId} was not found.", approverUserId);
                throw new EntityNotFoundException("Approver user not found.");
            }

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

                        // Fire-and-forget: notify the document uploader that their document was approved.
                        var uploader = await _context.Users.FindAsync(document.CreatedByUserId);
                        if (uploader != null)
                        {
                            _ = Task.Run(() => _emailService.SendDocumentApprovedToUploaderAsync(
                                uploaderEmail:        uploader.Email,
                                uploaderName:         uploader.Name,
                                approverName:         currentApprover.Name,
                                documentTitle:        document.Title,
                                documentDescription:  document.Description,
                                targetDepartmentName: document.TargetDepartment?.Name ?? string.Empty,
                                documentId:           document.Id,
                                approvedAt:           actedAt));
                        }

                        _logger.LogInformation("Document {DocumentId} successfully approved entirely by approver {ApproverId}.", documentId, approverUserId);
                        break;
                    }

                case ApproveDocumentAction.ApproveAndForward:
                    {
                        if (!currentApprover.ManagerId.HasValue)
                        {
                            _logger.LogWarning("Approval forward failed: Current approver {ApproverId} has no manager.", approverUserId);
                            throw new InvalidOperationException(
                                "Cannot forward: the current approver has no manager in this department.");
                        }

                        var manager = await _context.Users
                            .Include(u => u.Department)
                            .FirstOrDefaultAsync(u => u.Id == currentApprover.ManagerId.Value);

                        if (manager == null)
                        {
                            _logger.LogWarning("Approval forward failed: Manager with ID {ManagerId} was not found.", currentApprover.ManagerId.Value);
                            throw new EntityNotFoundException("Manager user not found.");
                        }

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

                        _logger.LogInformation("Document {DocumentId} approved by {ApproverId} and forwarded to manager {ManagerId}.", documentId, approverUserId, manager.Id);
                        break;
                    }

                case ApproveDocumentAction.ApproveAndTransfer:
                    {
                        if (!request.TargetUserId.HasValue)
                        {
                            _logger.LogWarning("Approval transfer failed: TargetUserId is missing.");
                            throw new ArgumentException(
                                "TargetUserId is required when action is ApproveAndTransfer.");
                        }

                        var targetUser = await _context.Users
                            .Include(u => u.Department)
                            .FirstOrDefaultAsync(u => u.Id == request.TargetUserId.Value);

                        if (targetUser == null)
                        {
                            _logger.LogWarning("Approval transfer failed: Target user with ID {TargetId} not found.", request.TargetUserId.Value);
                            throw new EntityNotFoundException(
                                $"Target user with ID {request.TargetUserId.Value} was not found.");
                        }

                        if (targetUser.DepartmentId == currentApprover.DepartmentId)
                        {
                            _logger.LogWarning("Approval transfer failed: Target user {TargetId} belongs to the same department as approver {ApproverId}.", targetUser.Id, approverUserId);
                            throw new InvalidOperationException(
                                "ApproveAndTransfer requires the target user to belong to a different department. " +
                                "Use ApproveAndForward to escalate within the same department.");
                        }

                        document.CurrentApproverUserId = targetUser.Id;
                        document.DocumentStatus = DocumentStatus.PendingApproval;

                        _context.ApprovalActions.Add(new ApprovalAction
                        {
                            DocumentId = document.Id,
                            DocumentVersionId = currentVersion.Id,
                            ApproverUserId = approverUserId,
                            Action = ApprovalActionType.Transfered,
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

                        _logger.LogInformation("Document {DocumentId} approved by {ApproverId} and transferred cross-department to user {TargetId}.", documentId, approverUserId, targetUser.Id);
                        break;
                    }

                default:
                    _logger.LogWarning("Approval failed: Unknown approval action '{Action}'.", request.Action);
                    throw new ArgumentException($"Unknown approval action '{request.Action}'.");
            }

            await _context.SaveChangesAsync();
            return response;
        }

        public async Task<ApproveDocumentResponseDto> TransferDocumentAsync(
            int documentId, TransferDocumentRequestDto request, int currentApproverUserId)
        {
            var document = await _context.Documents
                .Include(d => d.TargetDepartment)
                .Include(d => d.Versions)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null)
            {
                _logger.LogWarning("Transfer failed: Document with ID {DocumentId} not found.", documentId);
                throw new EntityNotFoundException($"Document with ID {documentId} was not found.");
            }

            if (document.CurrentApproverUserId != currentApproverUserId)
            {
                _logger.LogWarning("Transfer failed: User {UserId} is not the current approver for document {DocumentId}.", currentApproverUserId, documentId);
                throw new UnauthorizedAccessException("You are not authorised to transfer this document.");
            }

            if (document.DocumentStatus != DocumentStatus.PendingApproval)
            {
                _logger.LogWarning("Transfer failed: Document {DocumentId} is in status '{Status}'. Only PendingApproval documents can be transferred.", documentId, document.DocumentStatus);
                throw new InvalidOperationException(
                    $"Only documents with status 'PendingApproval' can be transferred. " +
                    $"Current status is '{document.DocumentStatus}'.");
            }

            if (request.TargetUserId == currentApproverUserId)
            {
                _logger.LogWarning("Transfer failed: User {UserId} attempted to transfer document {DocumentId} to themselves.", currentApproverUserId, documentId);
                throw new ArgumentException("Cannot transfer the document to yourself.");
            }

            var targetUser = await _context.Users
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.Id == request.TargetUserId);

            if (targetUser == null)
            {
                _logger.LogWarning("Transfer failed: Target user with ID {TargetId} not found.", request.TargetUserId);
                throw new EntityNotFoundException(
                    $"Target user with ID {request.TargetUserId} was not found.");
            }

            var currentApprover = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == currentApproverUserId);

            if (currentApprover == null)
            {
                _logger.LogWarning("Transfer failed: Current approver with ID {ApproverId} not found.", currentApproverUserId);
                throw new EntityNotFoundException("Current approver user not found.");
            }

            var currentVersion = document.Versions.FirstOrDefault(v => v.IsCurrentVersion)
                ?? document.Versions.OrderByDescending(v => v.VersionNumber).First();

            var actedAt = DateTimeOffset.UtcNow;
            bool isCrossDept = targetUser.DepartmentId != currentApprover.DepartmentId;

            document.CurrentApproverUserId = targetUser.Id;

            _context.ApprovalActions.Add(new ApprovalAction
            {
                DocumentId = document.Id,
                DocumentVersionId = currentVersion.Id,
                ApproverUserId = currentApproverUserId,
                Action = ApprovalActionType.Forwarded,
                ForwardedToDepartmentId = isCrossDept ? targetUser.DepartmentId : null,
                Comments = request.Comments,
                CreatedAt = actedAt
            });

            _context.AuditLogs.Add(new AuditLog
            {
                PerformedByUserId = currentApproverUserId,
                DocumentId = document.Id,
                DocumentVersionId = currentVersion.Id,
                Action = AuditAction.DocumentForwarded,
                Details = isCrossDept
                    ? $"Document transferred to '{targetUser.Name}' in department '{targetUser.Department?.Name}' (without approval)."
                    : $"Document transferred to '{targetUser.Name}' in the same department (without approval).",
                CreatedAt = actedAt
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Document {DocumentId} transferred by user {UserId} to target user {TargetId}.", documentId, currentApproverUserId, targetUser.Id);

            return new ApproveDocumentResponseDto
            {
                DocumentId = document.Id,
                Title = document.Title,
                DocumentStatus = document.DocumentStatus.ToString(),
                ActionTaken = "TransferWithoutApproval",
                NewApproverUserId = targetUser.Id,
                NewApproverName = targetUser.Name,
                NewApproverDepartmentName = targetUser.Department?.Name,
                ForwardedToDepartmentId = isCrossDept ? targetUser.DepartmentId : null,
                ForwardedToDepartmentName = isCrossDept ? targetUser.Department?.Name : null,
                ApproverComments = request.Comments,
                ActedAt = actedAt
            };
        }

        public async Task<DocumentFileDto> GetDocumentFileAsync(
            int documentId, int requestingUserId, int? versionId = null)
        {
            var document = await _context.Documents
                .Include(d => d.Versions)
                .Include(d => d.ApprovalActions)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null)
            {
                _logger.LogWarning("Download failed: Document with ID {DocumentId} not found.", documentId);
                throw new EntityNotFoundException($"Document with ID {documentId} was not found.");
            }

            var requestingUser = await _context.Users.FindAsync(requestingUserId);
            if (requestingUser == null)
            {
                _logger.LogWarning("Download failed: Requesting user with ID {UserId} not found.", requestingUserId);
                throw new EntityNotFoundException("Requesting user not found.");
            }

            try
            {
                AssertCanViewDocument(document, requestingUser);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Download unauthorized: User {UserId} is not authorized to view document {DocumentId}.", requestingUserId, documentId);
                throw;
            }

            DocumentVersion? version;

            if (versionId.HasValue)
            {
                version = document.Versions.FirstOrDefault(v => v.Id == versionId.Value);
                if (version == null)
                {
                    _logger.LogWarning("Download failed: Version with ID {VersionId} not found on document {DocumentId}.", versionId.Value, documentId);
                    throw new EntityNotFoundException(
                        $"Version with ID {versionId.Value} was not found on document {documentId}.");
                }
            }
            else
            {
                version = document.Versions.FirstOrDefault(v => v.IsCurrentVersion)
                    ?? document.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

                if (version == null)
                {
                    _logger.LogWarning("Download failed: No version found for document {DocumentId}.", documentId);
                    throw new EntityNotFoundException($"No version found for document {documentId}.");
                }
            }

            Stream fileStream;
            try
            {
                fileStream = await _blobStorageService.DownloadFileAsync(version.StoredFileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Download failed: Blob for version {Version} of document {DocumentId} was not found on Azure.", version.VersionNumber, documentId);
                throw new EntityNotFoundException(
                    $"File for version {version.VersionNumber} of document {documentId} " +
                    "was not found on the server.");
            }

            _context.AuditLogs.Add(new AuditLog
            {
                PerformedByUserId = requestingUserId,
                DocumentId = document.Id,
                DocumentVersionId = version.Id,
                Action = AuditAction.DocumentDownloaded,
                Details = $"Version {version.VersionNumber} viewed/downloaded.",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Physical file for version {Version} of document {DocumentId} successfully downloaded by user {UserId}.", version.VersionNumber, documentId, requestingUserId);

            return new DocumentFileDto
            {
                FileStream = fileStream,
                MimeType = version.MimeType,
                OriginalFileName = version.OriginalFileName,
                VersionNumber = version.VersionNumber
            };
        }

        private static void AssertCanViewDocument(Document document, User requestingUser)
        {
            bool isUploader = document.CreatedByUserId == requestingUser.Id;
            bool isCurrentApprover = document.CurrentApproverUserId == requestingUser.Id;
            bool isPastApprover = document.ApprovalActions.Any(aa => aa.ApproverUserId == requestingUser.Id);
            bool isAdmin = requestingUser.IsAdmin;

            if (!isUploader && !isCurrentApprover && !isPastApprover && !isAdmin)
                throw new UnauthorizedAccessException("You are not authorised to view this document.");
        }

        /// <summary>
        /// Computes the SHA-256 hash of the uploaded file content and returns it as an uppercase hex string.
        /// The stream is rewound to position 0 before reading so the caller can still use the file afterwards.
        /// </summary>
        private static async Task<string> ComputeSha256HashAsync(IFormFile file)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var stream = file.OpenReadStream();
            byte[] hashBytes = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hashBytes); // 64 uppercase hex chars
        }
    }
}
