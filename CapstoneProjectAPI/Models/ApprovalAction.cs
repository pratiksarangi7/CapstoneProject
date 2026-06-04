using CapstoneProjectAPI.Models.Enums;

namespace CapstoneProjectAPI.Models
{
    /// <summary>
    /// Represents the valid actions for the approval workflow.
    /// </summary>
    public class ApprovalAction
    {
        public int Id { get; set; }
        public int DocumentId { get; set; }
        public int DocumentVersionId { get; set; }
        public int ApproverUserId { get; set; }
        public ApprovalActionType Action { get; set; }
        public int? ForwardedToDepartmentId { get; set; }
        public string? Comments { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation properties
        public Document Document { get; set; } = null!;
        public DocumentVersion DocumentVersion { get; set; } = null!;
        public User ApproverUser { get; set; } = null!;
        public Department? ForwardedToDepartment { get; set; }
    }
}