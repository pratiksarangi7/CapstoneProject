using System.Numerics;
using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Exceptions;
using CapstoneProjectAPI.Misc;
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

        private const long MaxFileSizeBytes = 5 * 1024 * 1024;

        public DocumentService(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<UploadDocumentResponseDto> UploadDocument(UploadDocumentRequestDto request, int uploaderUserId)
        {
            if (request.File == null || request.File.Length == 0)
                throw new ArgumentException("File is required.");

            if (request.File.Length > MaxFileSizeBytes)
                throw new ArgumentException("File size exceeds the maximum allowed size of 5 MB.");

            string mimeType = request.File.ContentType;
            if (!AllowedMimeTypes.Contains(mimeType))
                throw new ArgumentException($"Invalid file type '{mimeType}'. Allowed types: PDF, JPEG, PNG.");

            var uploader = await _context.Users.FindAsync(uploaderUserId)
                ?? throw new EntityNotFoundException("Uploader user not found.");

            var targetDeptExists = await _context.Departments.AnyAsync(d => d.Id == request.TargetDepartmentId);
            if (!targetDeptExists)
                throw new EntityNotFoundException("Target department not found.");

            int? approverUserId = await DetermineFirstApprover(uploader, request.TargetDepartmentId);

            string uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads");
            Directory.CreateDirectory(uploadsFolder);

            string storedFileName = $"{Guid.NewGuid()}{Path.GetExtension(request.File.FileName)}";
            string filePath = Path.Combine(uploadsFolder, storedFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }

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
                IsCurrentVersion = true,
                UploadedByUserId = uploaderUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.DocumentVersions.Add(documentVersion);
            await _context.SaveChangesAsync();

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
                .Include(d => d.Versions)
                    .ThenInclude(v => v.AuditLogs)
                .Include(d => d.AuditLogs)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (document == null)
                throw new EntityNotFoundException($"Document with ID {documentId} was not found.");

            if (document.CreatedByUserId != requestingUserId)
                throw new UnauthorizedAccessException("You are not authorised to withdraw this document.");

            if (document.DocumentStatus != DocumentStatus.PendingApproval)
                throw new InvalidOperationException(
                    $"Document cannot be withdrawn because its status is '{document.DocumentStatus}'. " +
                    "Only documents with status 'PendingApproval' can be withdrawn.");

            foreach (var version in document.Versions)
            {
                if (version.AuditLogs.Any())
                    _context.AuditLogs.RemoveRange(version.AuditLogs);
            }

            if (document.AuditLogs.Any())
                _context.AuditLogs.RemoveRange(document.AuditLogs);

            string uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads");
            foreach (var version in document.Versions)
            {
                string filePath = Path.Combine(uploadsFolder, version.StoredFileName);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
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
                IsCurrentVersion = version.IsCurrentVersion,
                UploadedByUserId = version.UploadedByUserId,
                UploadedByUserName = version.UploadedByUser?.Name ?? string.Empty,
                CreatedAt = version.CreatedAt,
                ApprovalAction = MapApprovalAction(latestAction)
            };
        }

        private static UserDocumentResponseDto MapUserDocument(Document document)
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

        public async Task<PagedResult<UserDocumentResponseDto>> GetDocumentsUploadedByUserAsync(int userId, int pageNumber = 1, int pageSize = 10)
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
                .Where(d => d.CreatedByUserId == userId)
                .OrderByDescending(d => d.CreatedAt);
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

        public async Task<PagedResult<UserDocumentResponseDto>> GetDocumentsPendingApprovalByUserAsync(int userId, int pageNumber = 1, int pageSize = 10)
        {
            if(pageNumber<1) pageNumber=1;
            if(pageSize<1 || pageSize>100) pageSize=10;
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
                throw new EntityNotFoundException("User not found.");

            var query =  _context.Documents
                .Include(d => d.TargetDepartment)
                .Include(d => d.CreatedByUser)
                .Include(d => d.CurrentApprover)
                    .ThenInclude(u => u!.Department)
                .Include(d => d.Versions)
                    .ThenInclude(v => v.UploadedByUser)
                .Include(d => d.Versions)
                    .ThenInclude(v => v.ApprovalActions)
                        .ThenInclude(aa => aa.ApproverUser)
                .Where(d => d.CurrentApproverUserId == userId && d.DocumentStatus == DocumentStatus.PendingApproval)
                .OrderByDescending(d => d.CreatedAt);
            int totalCount=await query.CountAsync();
            var documents=await query.Skip((pageNumber-1)*pageSize).Take(pageSize).ToListAsync();
            return new PagedResult<UserDocumentResponseDto>()
            {
                Items = documents.Select(MapUserDocument).ToList(),
                PageNumber=pageNumber,
                PageSize=pageSize,
                TotalCount=totalCount
            };
        }

        public async Task RejectDocumentAsync(int documentId, int approverUserId, string reason)
        {
            var document = await _context.Documents
                .Include(d => d.Versions)
                .FirstOrDefaultAsync(d => d.Id == documentId)
                ?? throw new EntityNotFoundException($"Document with ID {documentId} was not found.");

            if (document.CurrentApproverUserId != approverUserId)
                throw new UnauthorizedAccessException("You are not authorised to reject this document.");

            if (document.DocumentStatus != DocumentStatus.PendingApproval)
                throw new InvalidOperationException(
                    $"Document cannot be rejected because its status is '{document.DocumentStatus}'. " +
                    "Only documents with status 'PendingApproval' can be rejected.");

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
        }

        public async Task<UploadDocumentResponseDto> ReUploadDocumentVersionAsync(
            int documentId, ReUploadDocumentRequestDto request, int uploaderUserId)
        {
            if (request.File == null || request.File.Length == 0)
                throw new ArgumentException("File is required.");

            if (request.File.Length > MaxFileSizeBytes)
                throw new ArgumentException("File size exceeds the maximum allowed size of 5 MB.");

            string mimeType = request.File.ContentType;
            if (!AllowedMimeTypes.Contains(mimeType))
                throw new ArgumentException($"Invalid file type '{mimeType}'. Allowed types: PDF, JPEG, PNG.");

            var document = await _context.Documents
                .Include(d => d.Versions)
                .FirstOrDefaultAsync(d => d.Id == documentId)
                ?? throw new EntityNotFoundException($"Document with ID {documentId} was not found.");

            if (document.CreatedByUserId != uploaderUserId)
                throw new UnauthorizedAccessException("You are not authorised to re-upload this document.");

            if (document.DocumentStatus != DocumentStatus.Rejected)
                throw new InvalidOperationException(
                    $"A new version can only be uploaded for rejected documents. " +
                    $"Current status is '{document.DocumentStatus}'.");

            var uploader = await _context.Users.FindAsync(uploaderUserId)
                ?? throw new EntityNotFoundException("Uploader user not found.");

            foreach (var v in document.Versions)
                v.IsCurrentVersion = false;

            string uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads");
            Directory.CreateDirectory(uploadsFolder);

            string storedFileName = $"{Guid.NewGuid()}{Path.GetExtension(request.File.FileName)}";
            string filePath = Path.Combine(uploadsFolder, storedFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
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

        public async Task<ApproveDocumentResponseDto> TransferDocumentAsync(
            int documentId, TransferDocumentRequestDto request, int currentApproverUserId)
        {
            var document = await _context.Documents
                .Include(d => d.TargetDepartment)
                .Include(d => d.Versions)
                .FirstOrDefaultAsync(d => d.Id == documentId)
                ?? throw new EntityNotFoundException($"Document with ID {documentId} was not found.");

            if (document.CurrentApproverUserId != currentApproverUserId)
                throw new UnauthorizedAccessException("You are not authorised to transfer this document.");

            if (document.DocumentStatus != DocumentStatus.PendingApproval)
                throw new InvalidOperationException(
                    $"Only documents with status 'PendingApproval' can be transferred. " +
                    $"Current status is '{document.DocumentStatus}'.");

            if (request.TargetUserId == currentApproverUserId)
                throw new ArgumentException("Cannot transfer the document to yourself.");

            var targetUser = await _context.Users
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.Id == request.TargetUserId)
                ?? throw new EntityNotFoundException(
                    $"Target user with ID {request.TargetUserId} was not found.");

            var currentApprover = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == currentApproverUserId)
                ?? throw new EntityNotFoundException("Current approver user not found.");

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
                .FirstOrDefaultAsync(d => d.Id == documentId)
                ?? throw new EntityNotFoundException($"Document with ID {documentId} was not found.");

            var requestingUser = await _context.Users.FindAsync(requestingUserId)
                ?? throw new EntityNotFoundException("Requesting user not found.");

            AssertCanViewDocument(document, requestingUser);

            DocumentVersion version;

            if (versionId.HasValue)
            {
                version = document.Versions.FirstOrDefault(v => v.Id == versionId.Value)
                    ?? throw new EntityNotFoundException(
                        $"Version with ID {versionId.Value} was not found on document {documentId}.");
            }
            else
            {
                version = document.Versions.FirstOrDefault(v => v.IsCurrentVersion)
                    ?? document.Versions.OrderByDescending(v => v.VersionNumber).First();
            }

            string uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads");
            string filePath = Path.Combine(uploadsFolder, version.StoredFileName);

            if (!File.Exists(filePath))
                throw new EntityNotFoundException(
                    $"Physical file for version {version.VersionNumber} of document {documentId} " +
                    "was not found on the server.");

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

            return new DocumentFileDto
            {
                FilePath = filePath,
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
    }
}
