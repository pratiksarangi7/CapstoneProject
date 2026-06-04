using CapstoneProjectAPI.Models.DTOs;

namespace CapstoneProjectAPI.Interfaces
{
    public interface IAuthenticationService
    {
        /// <summary>
        /// Registers a new user. Hashes the password and persists the user.
        /// Returns the saved user details (without a token).
        /// </summary>
        Task<RegisterResponseDto> Register(RegisterRequestDto request);

        /// <summary>
        /// Validates credentials and, if correct, returns a signed JWT token
        /// together with basic user info.
        /// </summary>
        Task<LoginResponseDto> Login(LoginRequestDto request);
    }
}