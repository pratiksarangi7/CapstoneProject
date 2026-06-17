using System.Security.Claims;
using CapstoneProjectAPI.Filters;
using CapstoneProjectAPI.Interfaces;
using CapstoneProjectAPI.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CapstoneProjectAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [EnableRateLimiting("FixedPerUser")]
    [TypeFilter(typeof(UserActiveFilter))]

    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUserDetails()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            var userDetails = await _userService.GetUserDetailsAsync(userId);
            return Ok(userDetails);
        }

        [HttpPut("me/change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            await _userService.ChangePasswordAsync(userId, request);
            return Ok(new { message = "Password changed successfully." });
        }

        [AllowAnonymous]
        [HttpGet("departments")]
        public async Task<IActionResult> GetDepartments()
        {
            var departments = await _userService.GetDepartmentsAsync();
            return Ok(departments);
        }
    }
}
