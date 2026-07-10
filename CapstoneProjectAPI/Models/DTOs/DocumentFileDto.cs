using System.IO;

namespace CapstoneProjectAPI.Models.DTOs
{
    public class DocumentFileDto
    {
        public Stream? FileStream { get; set; }
        public string MimeType { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
    }
}
