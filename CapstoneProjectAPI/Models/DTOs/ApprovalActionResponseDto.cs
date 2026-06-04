using System;

namespace CapstoneProjectAPI.Models.DTOs
{
    public class ApprovalActionResponseDto
    {
        public int Id { get; set; }
        public int ApproverUserId { get; set; }
        public string ApproverUserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? Comments { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
