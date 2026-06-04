using CapstoneProjectAPI.Models.DTOs;

namespace CapstoneProjectAPI.Interfaces
{
    public interface IUserService
    {
        Task<UserDetailsResponseDto> GetUserDetailsAsync(int userId);
    }
}
