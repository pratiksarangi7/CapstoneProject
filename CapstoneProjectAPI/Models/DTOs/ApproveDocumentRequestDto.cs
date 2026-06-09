using System.ComponentModel.DataAnnotations;

namespace CapstoneProjectAPI.Models.DTOs
{
    public enum ApproveDocumentAction
    {
        ApproveEntirely,

        ApproveAndForward,

        ApproveAndTransfer
    }

    public class ApproveDocumentRequestDto
    {
        [Required]
        public ApproveDocumentAction Action { get; set; }

        public int? TargetUserId { get; set; }

        [MaxLength(500)]
        public string? Comments { get; set; }
    }
}
