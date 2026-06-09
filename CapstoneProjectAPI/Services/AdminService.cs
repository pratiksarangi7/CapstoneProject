using AutoMapper;
using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Exceptions;
using CapstoneProjectAPI.Misc;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace CapstoneProjectAPI.Services
{
    public class AdminService
    {
        public AppDbContext _context;
        private readonly IMapper _mapper;

        public AdminService(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        public async Task<PagedResult<UserDetailsResponseDto>> GetUsers(int pageNumber = 1, int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;
            var query = _context.Users
                .Include(u => u.Department)
                .Include(u => u.Manager)
                .OrderBy(u => u.Id);
            int totalCount = await query.CountAsync();
            var users = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
            var mappedUsers = _mapper.Map<List<UserDetailsResponseDto>>(users);

            return new PagedResult<UserDetailsResponseDto>
            {
                Items = mappedUsers,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<bool> ChangeUserManager(ChangeUserManagerRequestDto request)
        {
            var user = await _context.Users.FindAsync(request.UserId)
                ?? throw new EntityNotFoundException("User not found");

            if (request.ManagerId.HasValue)
            {
                var manager = await _context.Users.FindAsync(request.ManagerId.Value)
                    ?? throw new EntityNotFoundException("Manager not found");

                if (request.ManagerId.Value == request.UserId)
                {
                    throw new ArgumentException("A user cannot be their own manager.");
                }
            }

            user.ManagerId = request.ManagerId;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangeUserLevel(ChangeUserLevelRequestDto request)
        {
            if (request.Level < 1)
            {
                throw new ArgumentException("Level must be a positive integer.");
            }

            var user = await _context.Users.FindAsync(request.UserId)
                ?? throw new EntityNotFoundException("User not found");

            user.Level = request.Level;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangeUserDepartment(ChangeUserDepartmentRequestDto request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
            {
                throw new EntityNotFoundException("User not found");
            }

            var departmentExists = await _context.Departments.AnyAsync(d => d.Id == request.DepartmentId);
            if (!departmentExists)
            {
                throw new EntityNotFoundException("Department not found");
            }

            user.DepartmentId = request.DepartmentId;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Department> AddDepartment(AddDepartmentRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Department name cannot be empty.");
            }

            var departmentExists = await _context.Departments
                .AnyAsync(d => d.Name.ToLower() == request.Name.ToLower());

            if (departmentExists)
            {
                throw new InvalidOperationException($"A department with name '{request.Name}' already exists.");
            }

            var department = new Department
            {
                Name = request.Name
            };

            _context.Departments.Add(department);
            await _context.SaveChangesAsync();

            return department;
        }
        public async Task<PagedResult<UserDocumentResponseDto>> GetAllDocuments(int pageNumber = 1, int pageSize = 10)
        {
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
                        .OrderByDescending(d => d.CreatedAt);
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

        public async Task<List<DepartmentResponseDto>> GetDepartments()
        {
            var departments = await _context.Departments
                .Include(d => d.Users)
                    .ThenInclude(u => u.Manager)
                .Select(d => new DepartmentResponseDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Users = d.Users.Select(u => new DepartmentUserDto
                    {
                        Id = u.Id,
                        Name = u.Name,
                        Email = u.Email,
                        IsAdmin = u.IsAdmin,
                        ManagerId = u.ManagerId,
                        ManagerName = u.Manager != null ? u.Manager.Name : null,
                        Level = u.Level
                    }).ToList()
                })
                .ToListAsync();
            return departments;
        }

        public async Task<bool> ReassignDocuments(ReassignDocumentsRequestDto request, int adminUserId)
        {
            var fromApprover = await _context.Users.FindAsync(request.FromApproverId)
                ?? throw new EntityNotFoundException("Source approver user not found");

            var toApprover = await _context.Users.FindAsync(request.ToApproverId)
                ?? throw new EntityNotFoundException("Target approver user not found");

            var query = _context.Documents
                .Include(d => d.CreatedByUser)
                .Include(d => d.Versions)
                .Where(d => d.CurrentApproverUserId == request.FromApproverId && d.DocumentStatus == DocumentStatus.PendingApproval);

            if (request.DocumentId.HasValue)
            {
                query = query.Where(d => d.Id == request.DocumentId.Value);
            }

            var documents = await query.ToListAsync();

            if (request.DocumentId.HasValue && !documents.Any())
            {
                throw new EntityNotFoundException($"No pending document with ID {request.DocumentId.Value} found for the source approver.");
            }

            foreach (var doc in documents)
            {
                if (toApprover.Level < doc.CreatedByUser.Level)
                {
                    throw new ArgumentException($"New approver's level ({toApprover.Level}) is less than the uploader's level ({doc.CreatedByUser.Level}) for document ID {doc.Id}.");
                }

                doc.CurrentApproverUserId = toApprover.Id;
                doc.TargetDepartmentId = toApprover.DepartmentId;

                var currentVersion = doc.Versions.FirstOrDefault(v => v.IsCurrentVersion)
                    ?? doc.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

                _context.AuditLogs.Add(new AuditLog
                {
                    PerformedByUserId = adminUserId,
                    DocumentId = doc.Id,
                    DocumentVersionId = currentVersion?.Id,
                    Action = AuditAction.DocumentForwarded,
                    Details = $"Admin reassigned document from '{fromApprover.Name}' to '{toApprover.Name}'.",
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task DeactivateUser(int userId, int adminUserId)
        {
            var user = await _context.Users.FindAsync(userId)
                ?? throw new EntityNotFoundException($"User with ID {userId} was not found.");

            if (userId == adminUserId)
                throw new ArgumentException("An admin cannot deactivate their own account.");

            if (user.IsAdmin)
                throw new ArgumentException("Admin accounts cannot be deactivated. Remove admin privileges first.");

            if (!user.IsActive)
                throw new InvalidOperationException("User is already deactivated.");

            int pendingAsApprover = await _context.Documents
                .CountAsync(d => d.CurrentApproverUserId == userId
                              && d.DocumentStatus == DocumentStatus.PendingApproval);

            if (pendingAsApprover > 0)
                throw new InvalidOperationException(
                    $"User cannot be deactivated: they are the current approver for {pendingAsApprover} pending document(s). " +
                    "Use PUT /api/admin/reassign-documents to reassign them first.");

            int pendingAsUploader = await _context.Documents
                .CountAsync(d => d.CreatedByUserId == userId
                              && d.DocumentStatus == DocumentStatus.PendingApproval);

            if (pendingAsUploader > 0)
                throw new InvalidOperationException(
                    $"User cannot be deactivated: they have {pendingAsUploader} document(s) still awaiting approval. " +
                    "Those documents must be approved or rejected before this user can be deactivated.");

            int activeSubordinates = await _context.Users
                .CountAsync(u => u.ManagerId == userId && u.IsActive);

            if (activeSubordinates > 0)
                throw new InvalidOperationException(
                    $"User cannot be deactivated: they are listed as the manager for {activeSubordinates} active user(s). " +
                    "Reassign those users to a different manager first (PUT /api/admin/change-manager).");

            user.IsActive = false;

            _context.AuditLogs.Add(new AuditLog
            {
                PerformedByUserId = adminUserId,
                Action = AuditAction.UserDeactivated,
                Details = $"Admin deactivated user '{user.Name}' (Email: {user.Email}).",
                CreatedAt = DateTimeOffset.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        public async Task ReactivateUser(int userId, int adminUserId)
        {
            var user = await _context.Users.FindAsync(userId)
                ?? throw new EntityNotFoundException($"User with ID {userId} was not found.");

            if (user.IsActive)
                throw new InvalidOperationException("User is already active.");

            user.IsActive = true;

            _context.AuditLogs.Add(new AuditLog
            {
                PerformedByUserId = adminUserId,
                Action = AuditAction.UserReactivated,
                Details = $"Admin reactivated user '{user.Name}' (Email: {user.Email}).",
                CreatedAt = DateTimeOffset.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        public async Task RejectAllPendingDocumentsOfUser(int userId, int adminUserId, string reason)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                throw new EntityNotFoundException($"User with ID {userId} was not found.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Reason is required.");
            }

            var pendingDocuments = await _context.Documents
                .Include(d => d.Versions)
                .Where(d => d.CreatedByUserId == userId && d.DocumentStatus == DocumentStatus.PendingApproval)
                .ToListAsync();

            if (!pendingDocuments.Any())
            {
                return;
            }

            var timestamp = DateTimeOffset.UtcNow;

            foreach (var document in pendingDocuments)
            {
                var currentVersion = document.Versions.FirstOrDefault(v => v.IsCurrentVersion)
                    ?? document.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

                if (currentVersion != null)
                {
                    _context.ApprovalActions.Add(new ApprovalAction
                    {
                        DocumentId = document.Id,
                        DocumentVersionId = currentVersion.Id,
                        ApproverUserId = adminUserId,
                        Action = ApprovalActionType.Rejected,
                        Comments = reason,
                        CreatedAt = timestamp
                    });
                }

                document.DocumentStatus = DocumentStatus.Rejected;
                document.CurrentApproverUserId = null;

                _context.AuditLogs.Add(new AuditLog
                {
                    PerformedByUserId = adminUserId,
                    DocumentId = document.Id,
                    DocumentVersionId = currentVersion?.Id,
                    Action = AuditAction.DocumentRejected,
                    Details = $"Document rejected by administrator. Reason: {reason}",
                    CreatedAt = timestamp
                });
            }

            await _context.SaveChangesAsync();
        }

    }
}