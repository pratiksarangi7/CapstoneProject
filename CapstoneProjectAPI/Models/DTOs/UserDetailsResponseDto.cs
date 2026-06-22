namespace CapstoneProjectAPI.Models.DTOs
{
    public class UserDetailsResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int? ManagerId { get; set; }
        public string? ManagerName { get; set; }
        public int Level { get; set; }
        public bool IsActive { get; set; }
    }
}
