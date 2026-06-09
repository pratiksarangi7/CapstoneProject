namespace CapstoneProjectAPI.Models.DTOs
{
    public class BulkUploadUserResultDto
    {
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<BulkUploadUserRowResult> Results { get; set; } = new();
    }

    public class BulkUploadUserRowResult
    {
        public int RowNumber { get; set; }
        public bool Success { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string? Error { get; set; }
    }
}
