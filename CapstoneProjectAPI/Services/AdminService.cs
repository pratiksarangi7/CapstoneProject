using System.Text;
using AutoMapper;
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
    public class AdminService
    {
        public AppDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<AdminService> _logger;

        public AdminService(AppDbContext context, IMapper mapper, ILogger<AdminService> logger)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
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
        public async Task<List<UserDetailsResponseDto>> GetPotentialManagers(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Get potential managers failed: User with ID {UserId} not found.", userId);
                throw new EntityNotFoundException("User not found");
            }

            var potentialManagers = await _context.Users
                .Include(u => u.Department)
                .Include(u => u.Manager)
                .Where(u => u.DepartmentId == user.DepartmentId
                         && u.Level > user.Level
                         && u.IsActive
                         && u.Id != userId)
                .OrderByDescending(u => u.Level)
                .ToListAsync();

            _logger.LogInformation(
                "Found {Count} potential manager(s) for user {UserId} in department {DeptId} with level > {Level}.",
                potentialManagers.Count, userId, user.DepartmentId, user.Level);

            return _mapper.Map<List<UserDetailsResponseDto>>(potentialManagers);
        }

        public async Task<bool> ChangeUserManager(ChangeUserManagerRequestDto request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
            {
                _logger.LogWarning("Change manager failed: User with ID {UserId} not found.", request.UserId);
                throw new EntityNotFoundException("User not found");
            }

            if (request.ManagerId.HasValue)
            {
                var manager = await _context.Users.FindAsync(request.ManagerId.Value);
                if (manager == null)
                {
                    _logger.LogWarning("Change manager failed: Manager user with ID {ManagerId} not found.", request.ManagerId.Value);
                    throw new EntityNotFoundException("Manager not found");
                }

                if (request.ManagerId.Value == request.UserId)
                {
                    _logger.LogWarning("Change manager failed: User with ID {UserId} attempted to assign themselves as their own manager.", request.UserId);
                    throw new ArgumentException("A user cannot be their own manager.");
                }
                if (manager.Level <= user.Level)
                {
                    _logger.LogWarning("Change manager failed: Proposed manager with ID {ManagerId} (Level {ManagerLevel}) must have a higher level than user with ID {UserId} (Level {UserLevel}).",
                            request.ManagerId.Value, manager.Level, request.UserId, user.Level);

                    throw new ArgumentException("A manager must have a strictly higher level than the user.");
                }
                if (manager.DepartmentId != user.DepartmentId)
                {
                    _logger.LogWarning("Change manager failed: User {UserId} (Dept {UserDeptId}) and manager {ManagerId} (Dept {ManagerDeptId}) must belong to the same department.",
                            request.UserId, user.DepartmentId, request.ManagerId.Value, manager.DepartmentId);

                    throw new ArgumentException("A user can only be assigned to a manager within their own department.");
                }
            }

            var oldManagerId = user.ManagerId;
            user.ManagerId = request.ManagerId;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully updated manager for user {UserId} from {OldManagerId} to {NewManagerId}.", request.UserId, oldManagerId, request.ManagerId);
            return true;
        }

        public async Task<bool> ChangeUserLevel(ChangeUserLevelRequestDto request)
        {
            if (request.Level < 1)
            {
                _logger.LogWarning("Change level failed: Level {Level} is invalid (must be >= 1).", request.Level);
                throw new ArgumentException("Level must be a positive integer.");
            }

            var user = await _context.Users
                .Include(u => u.Manager)
                .FirstOrDefaultAsync(u => u.Id == request.UserId);
            if (user == null)
            {
                _logger.LogWarning("Change level failed: User with ID {UserId} not found.", request.UserId);
                throw new EntityNotFoundException("User not found");
            }

            var oldLevel = user.Level;
            user.Level = request.Level;
            if (user.Manager != null && user.Manager.Level <= request.Level)
            {
                _logger.LogWarning("changing user level failed: user can't have level same as manager");
                throw new ArgumentException("Manager's level can't be same as user's level. Update manager's level first");
            }
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully changed level of user {UserId} from {OldLevel} to {NewLevel}.", request.UserId, oldLevel, request.Level);
            return true;
        }

        public async Task<bool> ChangeUserDepartment(ChangeUserDepartmentRequestDto request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
            {
                _logger.LogWarning("Change department failed: User with ID {UserId} not found.", request.UserId);
                throw new EntityNotFoundException("User not found");
            }

            var departmentExists = await _context.Departments.AnyAsync(d => d.Id == request.DepartmentId);
            if (!departmentExists)
            {
                _logger.LogWarning("Change department failed: Department with ID {DeptId} not found.", request.DepartmentId);
                throw new EntityNotFoundException("Department not found");
            }

            var oldDeptId = user.DepartmentId;
            user.DepartmentId = request.DepartmentId;
            user.ManagerId = null;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully changed department for user {UserId} from department {OldDeptId} to {NewDeptId}.", request.UserId, oldDeptId, request.DepartmentId);
            return true;
        }

        public async Task<Department> AddDepartment(AddDepartmentRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                _logger.LogWarning("Add department failed: Department name is empty or whitespace.");
                throw new ArgumentException("Department name cannot be empty.");
            }

            var departmentExists = await _context.Departments
                .AnyAsync(d => d.Name.ToLower() == request.Name.ToLower());

            if (departmentExists)
            {
                _logger.LogWarning("Add department failed: A department with name '{Name}' already exists.", request.Name);
                throw new InvalidOperationException($"A department with name '{request.Name}' already exists.");
            }

            var department = new Department
            {
                Name = request.Name
            };

            _context.Departments.Add(department);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully created new department '{Name}' with ID {DeptId}.", department.Name, department.Id);
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
            var fromApprover = await _context.Users.FindAsync(request.FromApproverId);
            if (fromApprover == null)
            {
                _logger.LogWarning("Reassign documents failed: Source approver user with ID {FromId} not found.", request.FromApproverId);
                throw new EntityNotFoundException("Source approver user not found");
            }

            var toApprover = await _context.Users.FindAsync(request.ToApproverId);
            if (toApprover == null)
            {
                _logger.LogWarning("Reassign documents failed: Target approver user with ID {ToId} not found.", request.ToApproverId);
                throw new EntityNotFoundException("Target approver user not found");
            }

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
                _logger.LogWarning("Reassign documents failed: No pending document with ID {DocId} found for source approver {FromId}.", request.DocumentId.Value, request.FromApproverId);
                throw new EntityNotFoundException($"No pending document with ID {request.DocumentId.Value} found for the source approver.");
            }

            foreach (var doc in documents)
            {
                if (toApprover.Level < doc.CreatedByUser.Level)
                {
                    _logger.LogWarning("Reassign documents failed: New approver {ToId} level ({ToLevel}) is less than uploader {UploaderId} level ({UploaderLevel}) for document {DocId}.",
                        toApprover.Id, toApprover.Level, doc.CreatedByUserId, doc.CreatedByUser.Level, doc.Id);
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

            _logger.LogInformation("Admin {AdminId} successfully reassigned {Count} documents from approver {FromId} to approver {ToId}.",
                adminUserId, documents.Count, request.FromApproverId, request.ToApproverId);
            return true;
        }

        public async Task DeactivateUser(int userId, int adminUserId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Deactivate user failed: User with ID {UserId} was not found.", userId);
                throw new EntityNotFoundException($"User with ID {userId} was not found.");
            }

            if (userId == adminUserId)
            {
                _logger.LogWarning("Deactivate user failed: Admin {AdminId} attempted to deactivate their own account.", adminUserId);
                throw new ArgumentException("An admin cannot deactivate their own account.");
            }

            if (user.IsAdmin)
            {
                _logger.LogWarning("Deactivate user failed: Attempted to deactivate admin account {UserId}.", userId);
                throw new ArgumentException("Admin accounts cannot be deactivated. Remove admin privileges first.");
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Deactivate user failed: User {UserId} is already deactivated.", userId);
                throw new InvalidOperationException("User is already deactivated.");
            }

            int pendingAsApprover = await _context.Documents
                .CountAsync(d => d.CurrentApproverUserId == userId
                              && d.DocumentStatus == DocumentStatus.PendingApproval);

            if (pendingAsApprover > 0)
            {
                _logger.LogWarning("Deactivate user failed: User {UserId} has {Count} pending document(s) to approve.", userId, pendingAsApprover);
                throw new InvalidOperationException(
                    $"User cannot be deactivated: they are the current approver for {pendingAsApprover} pending document(s). " +
                    "Use PUT /api/admin/reassign-documents to reassign them first.");
            }

            int pendingAsUploader = await _context.Documents
                .CountAsync(d => d.CreatedByUserId == userId
                              && d.DocumentStatus == DocumentStatus.PendingApproval);

            if (pendingAsUploader > 0)
            {
                _logger.LogWarning("Deactivate user failed: User {UserId} has {Count} uploaded document(s) pending approval.", userId, pendingAsUploader);
                throw new InvalidOperationException(
                    $"User cannot be deactivated: they have {pendingAsUploader} document(s) still awaiting approval. " +
                    "Those documents must be approved or rejected before this user can be deactivated.");
            }

            int activeSubordinates = await _context.Users
                .CountAsync(u => u.ManagerId == userId && u.IsActive);

            if (activeSubordinates > 0)
            {
                _logger.LogWarning("Deactivate user failed: User {UserId} is manager for {Count} active user(s).", userId, activeSubordinates);
                throw new InvalidOperationException(
                    $"User cannot be deactivated: they are listed as the manager for {activeSubordinates} active user(s). " +
                    "Reassign those users to a different manager first (PUT /api/admin/change-manager).");
            }

            user.IsActive = false;

            _context.AuditLogs.Add(new AuditLog
            {
                PerformedByUserId = adminUserId,
                Action = AuditAction.UserDeactivated,
                Details = $"Admin deactivated user '{user.Name}' (Email: {user.Email}).",
                CreatedAt = DateTimeOffset.UtcNow
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} successfully deactivated user {UserId} ('{Name}').", adminUserId, userId, user.Name);
        }

        public async Task ReactivateUser(int userId, int adminUserId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Reactivate user failed: User with ID {UserId} was not found.", userId);
                throw new EntityNotFoundException($"User with ID {userId} was not found.");
            }

            if (user.IsActive)
            {
                _logger.LogWarning("Reactivate user failed: User {UserId} is already active.", userId);
                throw new InvalidOperationException("User is already active.");
            }

            user.IsActive = true;

            _context.AuditLogs.Add(new AuditLog
            {
                PerformedByUserId = adminUserId,
                Action = AuditAction.UserReactivated,
                Details = $"Admin reactivated user '{user.Name}' (Email: {user.Email}).",
                CreatedAt = DateTimeOffset.UtcNow
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} successfully reactivated user {UserId} ('{Name}').", adminUserId, userId, user.Name);
        }

        public async Task RejectAllPendingDocumentsOfUser(int userId, int adminUserId, string reason)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                _logger.LogWarning("Reject all pending documents failed: User with ID {UserId} was not found.", userId);
                throw new EntityNotFoundException($"User with ID {userId} was not found.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                _logger.LogWarning("Reject all pending documents failed: Reason is missing.");
                throw new ArgumentException("Reason is required.");
            }

            var pendingDocuments = await _context.Documents
                .Include(d => d.Versions)
                .Where(d => d.CreatedByUserId == userId && d.DocumentStatus == DocumentStatus.PendingApproval)
                .ToListAsync();

            if (!pendingDocuments.Any())
            {
                _logger.LogInformation("No pending documents to reject for user {UserId}.", userId);
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

            _logger.LogInformation("Admin {AdminId} successfully rejected all ({Count}) pending documents of user {UserId}. Reason: {Reason}.",
                adminUserId, pendingDocuments.Count, userId, reason);
        }

        public async Task<BulkUploadUserResultDto> BulkUploadUsersAsync(IFormFile file, int adminUserId)
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("Bulk upload users failed: File is missing or empty.");
                throw new ArgumentException("CSV file is required.");
            }
            var allowedMimeTypes = new[] { "text/csv", "application/csv", "application/vnd.ms-excel" };

            if (!allowedMimeTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Bulk upload users failed: Invalid file type {Type}.", file.ContentType);
                throw new ArgumentException("Only .csv files are accepted.");
            }
            var result = new BulkUploadUserResultDto();
            var timestamp = DateTimeOffset.UtcNow;

            using var reader = new StreamReader(file.OpenReadStream());
            var allContent = await reader.ReadToEndAsync();
            var lines = allContent.Split('\n', StringSplitOptions.None);

            if (lines.Length == 0 || (lines.Length == 1 && string.IsNullOrWhiteSpace(lines[0])))
            {
                _logger.LogWarning("Bulk upload users failed: CSV file contains no lines.");
                throw new ArgumentException("CSV file is empty.");
            }

            int rowNumber = 1;
            foreach (var rawLine in lines.Skip(1))
            {
                var line = rawLine.Trim('\r', ' ');
                rowNumber++;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var columns = line.Split(',');

                if (columns.Length < 4)
                {
                    _logger.LogWarning("Bulk upload users row {Row}: Incorrect columns count ({Count}).", rowNumber, columns.Length);
                    result.Results.Add(new BulkUploadUserRowResult
                    {
                        RowNumber = rowNumber,
                        Success = false,
                        Error = "Row does not have all 4 required columns: Name, Email, Password, DepartmentId."
                    });
                    continue;
                }

                string name = columns[0].Trim();
                string email = columns[1].Trim();
                string password = columns[2].Trim();
                string deptIdRaw = columns[3].Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.LogWarning("Bulk upload users row {Row}: Name is empty.", rowNumber);
                    result.Results.Add(new BulkUploadUserRowResult { RowNumber = rowNumber, Success = false, Email = email, Error = "Name is required." });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogWarning("Bulk upload users row {Row}: Email is empty.", rowNumber);
                    result.Results.Add(new BulkUploadUserRowResult { RowNumber = rowNumber, Success = false, Error = "Email is required." });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
                {
                    _logger.LogWarning("Bulk upload users row {Row}: Password length must be >= 6.", rowNumber);
                    result.Results.Add(new BulkUploadUserRowResult { RowNumber = rowNumber, Success = false, Email = email, Name = name, Error = "Password must be at least 6 characters." });
                    continue;
                }

                if (!int.TryParse(deptIdRaw, out int departmentId) || departmentId <= 0)
                {
                    _logger.LogWarning("Bulk upload users row {Row}: Invalid department ID '{Raw}'.", rowNumber, deptIdRaw);
                    result.Results.Add(new BulkUploadUserRowResult { RowNumber = rowNumber, Success = false, Email = email, Name = name, Error = $"Invalid DepartmentId '{deptIdRaw}'." });
                    continue;
                }

                bool emailExists = await _context.Users.AnyAsync(u => u.Email == email);
                if (emailExists)
                {
                    _logger.LogWarning("Bulk upload users row {Row}: User with email {Email} already exists.", rowNumber, email);
                    result.Results.Add(new BulkUploadUserRowResult { RowNumber = rowNumber, Success = false, Email = email, Name = name, Error = $"A user with email '{email}' already exists." });
                    continue;
                }

                bool deptExists = await _context.Departments.AnyAsync(d => d.Id == departmentId);
                if (!deptExists)
                {
                    _logger.LogWarning("Bulk upload users row {Row}: Department with ID {DeptId} not found.", rowNumber, departmentId);
                    result.Results.Add(new BulkUploadUserRowResult { RowNumber = rowNumber, Success = false, Email = email, Name = name, Error = $"Department with ID {departmentId} was not found." });
                    continue;
                }

                var user = new User
                {
                    Name = name,
                    Email = email,
                    PasswordHash = HashPasswordForBulk(password),
                    DepartmentId = departmentId
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _context.AuditLogs.Add(new AuditLog
                {
                    PerformedByUserId = adminUserId,
                    Action = AuditAction.UserRegistered,
                    Details = $"Admin bulk-uploaded user '{name}' (Email: {email}).",
                    CreatedAt = timestamp
                });
                await _context.SaveChangesAsync();

                result.Results.Add(new BulkUploadUserRowResult
                {
                    RowNumber = rowNumber,
                    Success = true,
                    Email = email,
                    Name = name
                });
            }

            result.TotalRows = result.Results.Count;
            result.SuccessCount = result.Results.Count(r => r.Success);
            result.FailureCount = result.Results.Count(r => !r.Success);

            _logger.LogInformation("Admin {AdminId} bulk uploaded users from CSV: Total: {Total}, Succeeded: {Success}, Failed: {Failure}.",
                adminUserId, result.TotalRows, result.SuccessCount, result.FailureCount);

            return result;
        }

        private static string HashPasswordForBulk(string password)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256();
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return $"{Convert.ToBase64String(hmac.Key)}:{Convert.ToBase64String(hash)}";
        }
    }
}