namespace CapstoneProjectAPI.Models
{
    public class DocumentVersion
    {
        public int Id { get; set; }
        public int DocumentId { get; set; }
        public int VersionNumber { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string MimeType { get; set; } = string.Empty;
        public bool IsCurrentVersion { get; set; } = false;
        public int UploadedByUserId { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public Document Document { get; set; } = null!;
        public User UploadedByUser { get; set; } = null!;
        public ICollection<ApprovalAction> ApprovalActions { get; set; } = new List<ApprovalAction>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}