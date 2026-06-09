using CapstoneProjectAPI.Models.Enums;
using CapstoneProjectAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapstoneProjectAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AuditLogController : ControllerBase
    {
        private readonly AuditLogService _auditLogService;
        public AuditLogController(AuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }
        [HttpGet]
        public async Task<IActionResult> GetAuditActions(
            [FromQuery] AuditAction? action,
            [FromQuery] int? userId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var results = await _auditLogService.GetAuditLogs(action, userId, pageNumber, pageSize);
            return Ok(results);
        }

    }
}