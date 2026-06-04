using CapstoneProjectAPI.Services;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

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
        public async Task<IActionResult> GetUsers()
        {
            var users = await _adminService.GetUsers();
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
    }
}