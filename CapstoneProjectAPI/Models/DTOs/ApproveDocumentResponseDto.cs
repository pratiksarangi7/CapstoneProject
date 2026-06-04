namespace CapstoneProjectAPI.Models.DTOs
{
    public class ApproveDocumentResponseDto
    {
        public int DocumentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string DocumentStatus { get; set; } = string.Empty;
        public string ActionTaken { get; set; } = string.Empty;

        // Populated when the document is forwarded or transferred
        public int? NewApproverUserId { get; set; }
        public string? NewApproverName { get; set; }
        public string? NewApproverDepartmentName { get; set; }

        // Populated when transferred to a different department
        public int? ForwardedToDepartmentId { get; set; }
        public string? ForwardedToDepartmentName { get; set; }

        public string? ApproverComments { get; set; }
        public DateTimeOffset ActedAt { get; set; }
    }
}
