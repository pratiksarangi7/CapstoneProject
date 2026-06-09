using CapstoneProjectAPI.Models.Enums;

namespace CapstoneProjectAPI.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public int PerformedByUserId { get; set; }
        public int? DocumentId { get; set; }
        public int? DocumentVersionId { get; set; }
        public AuditAction Action { get; set; }
        public string? Details { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public User PerformedByUser { get; set; } = null!;
        public Document? Document { get; set; }
        public DocumentVersion? DocumentVersion { get; set; }
    }
}