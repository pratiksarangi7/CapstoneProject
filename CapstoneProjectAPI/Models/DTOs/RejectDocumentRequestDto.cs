using System.ComponentModel.DataAnnotations;

namespace CapstoneProjectAPI.Models.DTOs
{
    public class RejectDocumentRequestDto
    {
        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }
}
