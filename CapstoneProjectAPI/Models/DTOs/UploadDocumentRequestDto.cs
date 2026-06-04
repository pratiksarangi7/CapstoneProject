using Microsoft.AspNetCore.Http;

namespace CapstoneProjectAPI.Models.DTOs
{
    public class UploadDocumentRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int TargetDepartmentId { get; set; }
        public IFormFile File { get; set; } = null!;
    }
}
