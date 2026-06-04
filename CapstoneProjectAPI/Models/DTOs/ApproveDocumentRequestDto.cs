using System.ComponentModel.DataAnnotations;

namespace CapstoneProjectAPI.Models.DTOs
{
    /// <summary>
    /// The three approval actions an approver can take on a document.
    /// </summary>
    public enum ApproveDocumentAction
    {
        /// <summary>Final approval — no further review required.</summary>
        ApproveEntirely,

        /// <summary>Approve and pass up to the current approver's manager (same department).</summary>
        ApproveAndForward,

        /// <summary>Approve and transfer ownership to a specific user in a different department.</summary>
        ApproveAndTransfer
    }

    public class ApproveDocumentRequestDto
    {
        [Required]
        public ApproveDocumentAction Action { get; set; }

        /// <summary>
        /// Required only when Action == ApproveAndTransfer.
        /// The ID of the user in the target department to transfer the document to.
        /// </summary>
        public int? TargetUserId { get; set; }

        /// <summary>Optional remarks from the approver.</summary>
        [MaxLength(500)]
        public string? Comments { get; set; }
    }
}
