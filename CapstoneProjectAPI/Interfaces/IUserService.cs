using CapstoneProjectAPI.Misc;
using CapstoneProjectAPI.Models.DTOs;

namespace CapstoneProjectAPI.Interfaces
{
    public interface IUserService
    {
        Task<UserDetailsResponseDto> GetUserDetailsAsync(int userId);
        Task ChangePasswordAsync(int userId, ChangePasswordRequestDto request);
        Task<List<OnlyDepartmentResponseDto>> GetDepartmentsAsync();
        Task<List<ExternalUserResponseDto>> GetUsersOutsideDepartmentAsync(int currentUserId);
        Task<PagedResult<AuditLogResponseDto>> GetUserDocumentActionsAsync(int userId, int pageNumber, int pageSize);
    }
}
