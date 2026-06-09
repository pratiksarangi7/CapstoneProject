using CapstoneProjectAPI.Services;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CapstoneProjectAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]

    public class AdminController : ControllerBase
    {
        private readonly AdminService _adminService;

        public AdminController(AdminService adminService)
        {
            _adminService = adminService;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var users = await _adminService.GetUsers(pageNumber, pageSize);
            return Ok(users);
        }

        [HttpPut("change-manager")]
        public async Task<IActionResult> ChangeUserManager([FromBody] ChangeUserManagerRequestDto request)
        {
            var result = await _adminService.ChangeUserManager(request);
            return Ok(new { message = "User manager updated successfully." });
        }

        [HttpPut("change-level")]
        public async Task<IActionResult> ChangeUserLevel([FromBody] ChangeUserLevelRequestDto request)
        {

            await _adminService.ChangeUserLevel(request);
            return Ok(new { message = "User level updated successfully." });
        }

        [HttpPut("change-department")]
        public async Task<IActionResult> ChangeUserDepartment([FromBody] ChangeUserDepartmentRequestDto request)
        {

            var result = await _adminService.ChangeUserDepartment(request);
            return Ok(new { message = "User department updated successfully." });
        }

        [HttpPost("department")]
        public async Task<IActionResult> AddDepartment([FromBody] AddDepartmentRequestDto request)
        {
            var department = await _adminService.AddDepartment(request);
            return CreatedAtAction(nameof(AddDepartment), new { id = department.Id }, department);
        }

        [HttpGet("departments")]
        public async Task<IActionResult> GetDepartments()
        {

            var departments = await _adminService.GetDepartments();
            return Ok(departments);

        }

        [HttpGet("documents/all")]
        public async Task<IActionResult> GetAllDocuments([FromQuery] int pageNumber=1, int pageSize=10)
        {
            var documents = await _adminService.GetAllDocuments(pageNumber, pageSize);
            return Ok(documents);
        }

        [HttpPut("reassign-documents")]
        public async Task<IActionResult> ReassignDocuments([FromBody] ReassignDocumentsRequestDto request)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int adminUserId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            await _adminService.ReassignDocuments(request, adminUserId);
            return Ok(new { message = "Documents reassigned successfully." });
        }

        [HttpPut("users/{id:int}/deactivate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeactivateUser(int id)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int adminUserId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            await _adminService.DeactivateUser(id, adminUserId);
            return Ok(new { message = "User deactivated successfully." });
        }
    }
}