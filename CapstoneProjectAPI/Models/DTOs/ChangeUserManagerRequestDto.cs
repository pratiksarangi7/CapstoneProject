namespace CapstoneProjectAPI.Models.DTOs
{
    public class ChangeUserManagerRequestDto
    {
        public int UserId { get; set; }
        public int? ManagerId { get; set; }
    }
}
