namespace CapstoneProjectAPI.Models.DTOs
{
    public class ReassignDocumentsRequestDto
    {
        public int FromApproverId { get; set; }
        public int ToApproverId { get; set; }
        public int? DocumentId { get; set; }
    }
}
