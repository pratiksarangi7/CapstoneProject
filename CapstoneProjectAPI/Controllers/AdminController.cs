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
        public async Task<IActionResult> GetUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string search = "")
        {
            var users = await _adminService.GetUsers(pageNumber, pageSize, search);
            return Ok(users);
        }

        [HttpGet("users/{id:int}/potential-managers")]
        public async Task<IActionResult> GetPotentialManagers(int id)
        {
            var managers = await _adminService.GetPotentialManagers(id);
            return Ok(managers);
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
        public async Task<IActionResult> GetAllDocuments([FromQuery] int pageNumber = 1, int pageSize = 10)
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

        [HttpPut("users/{id:int}/reactivate")]
        public async Task<IActionResult> ReactivateUser(int id)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int adminUserId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            await _adminService.ReactivateUser(id, adminUserId);
            return Ok(new { message = "User reactivated successfully." });
        }

        [HttpPut("users/{id:int}/reject-pending-documents")]
        public async Task<IActionResult> RejectPendingDocumentsOfUser(int id, [FromBody] RejectDocumentRequestDto request)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int adminUserId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            await _adminService.RejectAllPendingDocumentsOfUser(id, adminUserId, request.Reason);
            return Ok(new { message = "All pending documents of the user have been rejected successfully." });
        }

        [HttpPost("users/bulk-upload")]
        [RequestSizeLimit(2 * 1024 * 1024)]
        public async Task<IActionResult> BulkUploadUsers([FromForm] IFormFile file)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int adminUserId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            var result = await _adminService.BulkUploadUsersAsync(file, adminUserId);
            return Ok(result);
        }
    }
}