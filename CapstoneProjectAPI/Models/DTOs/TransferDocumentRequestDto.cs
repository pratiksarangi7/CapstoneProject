using System.ComponentModel.DataAnnotations;

namespace CapstoneProjectAPI.Models.DTOs
{
    public class TransferDocumentRequestDto
    {
        /// <summary>
        /// The ID of the user to transfer responsibility to.
        /// Can be in the same or a different department.
        /// </summary>
        [Required]
        public int TargetUserId { get; set; }

        /// <summary>Optional reason for the transfer.</summary>
        [MaxLength(500)]
        public string? Comments { get; set; }
    }
}
