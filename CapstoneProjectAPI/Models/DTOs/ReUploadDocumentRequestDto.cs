using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace CapstoneProjectAPI.Models.DTOs
{
    public class ReUploadDocumentRequestDto
    {
        [Required]
        public IFormFile File { get; set; } = null!;
    }
}
