using System;

namespace CapstoneProjectAPI.Models.DTOs
{
    public class DocumentVersionResponseDto
    {
        public int Id { get; set; }
        public int DocumentId { get; set; }
        public int VersionNumber { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string MimeType { get; set; } = string.Empty;
        public bool IsCurrentVersion { get; set; }
        public int UploadedByUserId { get; set; }
        public string UploadedByUserName { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public ApprovalActionResponseDto? ApprovalAction { get; set; }
    }
}
