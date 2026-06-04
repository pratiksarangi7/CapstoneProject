namespace CapstoneProjectAPI.Models.DTOs
{
    public class DepartmentResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<DepartmentUserDto> Users { get; set; } = new();
    }

    public class DepartmentUserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public int? ManagerId { get; set; }
        public string? ManagerName { get; set; }
        public int Level { get; set; }
    }
}
