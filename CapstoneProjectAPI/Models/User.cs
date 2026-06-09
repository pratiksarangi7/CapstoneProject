namespace CapstoneProjectAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsAdmin { get; set; } = false;
        public int DepartmentId { get; set; }
        public int? ManagerId { get; set; }
        public int Level { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public Department Department { get; set; } = null!;
        public User? Manager { get; set; }
        public ICollection<User> Subordinates { get; set; } = new List<User>();
        public ICollection<Document> CreatedDocuments { get; set; } = new List<Document>();
        public ICollection<DocumentVersion> UploadedVersions { get; set; } = new List<DocumentVersion>();
        public ICollection<ApprovalAction> ApprovalActions { get; set; } = new List<ApprovalAction>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}