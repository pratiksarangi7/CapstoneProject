namespace CapstoneProjectAPI.Models.DTOs
{

    public class DocumentFileDto
    {
        public string FilePath { get; set; } = string.Empty;

        public string MimeType { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public int VersionNumber { get; set; }
    }
}
