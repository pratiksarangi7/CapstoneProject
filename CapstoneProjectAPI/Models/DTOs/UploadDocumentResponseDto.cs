namespace CapstoneProjectAPI.Models.DTOs
{
    public class UploadDocumentResponseDto
    {
        public int DocumentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DocumentStatus { get; set; } = string.Empty;
        public int TargetDepartmentId { get; set; }
        public int? CurrentApproverUserId { get; set; }
        public string? CurrentApproverName { get; set; }
        public int VersionNumber { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string MimeType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsExpired { get; set; }
    }
}
