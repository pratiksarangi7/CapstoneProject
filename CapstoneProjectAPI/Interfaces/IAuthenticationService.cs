using CapstoneProjectAPI.Models.DTOs;

namespace CapstoneProjectAPI.Interfaces
{
    public interface IAuthenticationService
    {
        Task<RegisterResponseDto> Register(RegisterRequestDto request);

        Task<LoginResponseDto> Login(LoginRequestDto request);
    }
}