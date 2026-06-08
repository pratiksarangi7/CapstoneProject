using System;

namespace CapstoneProjectAPI.Models.DTOs
{
    public class AuditLogResponseDto
    {
        public int Id { get; set; }
        public int PerformedByUserId { get; set; }
        public string PerformedByUserName { get; set; } = string.Empty;
        public string PerformedByUserEmail { get; set; } = string.Empty;
        public int? DocumentId { get; set; }
        public string? DocumentTitle { get; set; }
        public int? DocumentVersionId { get; set; }
        public int? DocumentVersionNumber { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
