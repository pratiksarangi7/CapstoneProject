using CapstoneProjectAPI.Models.Enums;

namespace CapstoneProjectAPI.Models
{
    public class Document
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CreatedByUserId { get; set; }
        public DocumentStatus DocumentStatus { get; set; }
        public int? CurrentApproverUserId { get; set; }
        public int TargetDepartmentId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiryDate { get; set; }
        public bool IsExpired { get; set; } = false;
        public User CreatedByUser { get; set; } = null!;
        public User? CurrentApprover { get; set; }
        public Department TargetDepartment { get; set; } = null!;
        public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
        public ICollection<ApprovalAction> ApprovalActions { get; set; } = new List<ApprovalAction>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}