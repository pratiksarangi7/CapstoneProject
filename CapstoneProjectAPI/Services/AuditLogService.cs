using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Misc;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace CapstoneProjectAPI.Services
{
    public class AuditLogService
    {
        private readonly AppDbContext _context;
        public AuditLogService(AppDbContext context)
        {
            _context = context;
        }
        public async Task<PagedResult<AuditLogResponseDto>> GetAuditLogs(AuditAction? action, int? userId = null, int pageNumber = 1, int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 0 || pageSize > 100) pageSize = 10;

            var query = _context.AuditLogs.AsQueryable();

            if (action.HasValue)
            {
                query = query.Where(al => al.Action == action.Value);
            }
            if (userId.HasValue)
            {
                query = query.Where(al => al.PerformedByUserId == userId);
            }

            query = query.Include(al => al.PerformedByUser)
                         .Include(al => al.Document)
                         .Include(al => al.DocumentVersion)
                         .OrderByDescending(al => al.CreatedAt);

            int totalCount = await query.CountAsync();
            var auditLogs = await query.Skip((pageNumber - 1) * pageSize)
                                    .Take(pageSize)
                                    .ToListAsync();

            var mappedItems = auditLogs.Select(al => new AuditLogResponseDto
            {
                Id = al.Id,
                PerformedByUserId = al.PerformedByUserId,
                PerformedByUserName = al.PerformedByUser != null ? al.PerformedByUser.Name : string.Empty,
                PerformedByUserEmail = al.PerformedByUser != null ? al.PerformedByUser.Email : string.Empty,
                DocumentId = al.DocumentId,
                DocumentTitle = al.Document != null ? al.Document.Title : null,
                DocumentVersionId = al.DocumentVersionId,
                DocumentVersionNumber = al.DocumentVersion != null ? (int?)al.DocumentVersion.VersionNumber : null,
                Action = al.Action.ToString(),
                Details = al.Details,
                CreatedAt = al.CreatedAt
            }).ToList();

            return new PagedResult<AuditLogResponseDto>()
            {
                Items = mappedItems,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
    }
}