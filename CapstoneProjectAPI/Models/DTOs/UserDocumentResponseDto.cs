using System;
using System.Collections.Generic;

namespace CapstoneProjectAPI.Models.DTOs
{
    public class UserDocumentResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DocumentStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public int TargetDepartmentId { get; set; }
        public string TargetDepartmentName { get; set; } = string.Empty;

        public int? CurrentApproverUserId { get; set; }
        public string? CurrentApproverName { get; set; }
        public string? CurrentApproverEmail { get; set; }
        public string? CurrentApproverDepartmentName { get; set; }

        public int CreatedByUserId { get; set; }
        public string CreatedByUserName { get; set; } = string.Empty;
        public string CreatedByUserEmail { get; set; } = string.Empty;

        public List<DocumentVersionResponseDto> Versions { get; set; } = new();
    }
}
