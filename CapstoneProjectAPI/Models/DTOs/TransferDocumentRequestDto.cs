using System.ComponentModel.DataAnnotations;

namespace CapstoneProjectAPI.Models.DTOs
{
    public class TransferDocumentRequestDto
    {
      
        [Required]
        public int TargetUserId { get; set; }

        [MaxLength(500)]
        public string? Comments { get; set; }
    }
}
