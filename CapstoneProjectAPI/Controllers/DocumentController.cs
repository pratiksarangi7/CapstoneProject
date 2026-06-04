using System.Security.Claims;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CapstoneProjectAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [EnableRateLimiting("FixedPerUser")]

    public class DocumentController : ControllerBase
    {
        private readonly DocumentService _documentService;

        public DocumentController(DocumentService documentService)
        {
            _documentService = documentService;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<IActionResult> Upload([FromForm] UploadDocumentRequestDto request)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int uploaderUserId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            var result = await _documentService.UploadDocument(request, uploaderUserId);
            return CreatedAtAction(nameof(Upload), new { id = result.DocumentId }, result);
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> WithdrawDocument(int id)
        {

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int requestingUserId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            await _documentService.WithdrawDocumentAsync(id, requestingUserId);
            return NoContent();


        }

        [HttpGet("my-uploads")]
        [ProducesResponseType(typeof(IEnumerable<UserDocumentResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMyUploadedDocuments([FromQuery] int pageNumber, [FromQuery] int pageSize)
        {

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            var documents = await _documentService.GetDocumentsUploadedByUserAsync(userId, pageNumber, pageSize);
            return Ok(documents);

        }

        [HttpGet("pending-approvals")]
        [ProducesResponseType(typeof(IEnumerable<UserDocumentResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDocumentsPendingApproval([FromQuery] int pageNumber, [FromQuery] int pageSize)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            var documents = await _documentService.GetDocumentsPendingApprovalByUserAsync(userId, pageNumber, pageSize);
            return Ok(documents);
        }

        [HttpPut("{id:int}/reject")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RejectDocument(int id, [FromBody] RejectDocumentRequestDto request)
        {

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int approverUserId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            await _documentService.RejectDocumentAsync(id, approverUserId, request.Reason);
            return NoContent();

        }

        [HttpPost("{id:int}/reupload")]
        [RequestSizeLimit(5 * 1024 * 1024)]
        [ProducesResponseType(typeof(UploadDocumentResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReUploadDocumentVersion(int id, [FromForm] ReUploadDocumentRequestDto request)
        {

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int uploaderUserId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            var result = await _documentService.ReUploadDocumentVersionAsync(id, request, uploaderUserId);
            return CreatedAtAction(nameof(ReUploadDocumentVersion), new { id = result.DocumentId }, result);

        }

        [HttpPut("{id:int}/approve")]
        [ProducesResponseType(typeof(ApproveDocumentResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ApproveDocument(int id, [FromBody] ApproveDocumentRequestDto request)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int approverUserId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            var result = await _documentService.ApproveDocumentAsync(id, request, approverUserId);
            return Ok(result);
        }

        [HttpPut("{id:int}/transfer")]
        [ProducesResponseType(typeof(ApproveDocumentResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TransferDocument(int id, [FromBody] TransferDocumentRequestDto request)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int approverUserId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            var result = await _documentService.TransferDocumentAsync(id, request, approverUserId);
            return Ok(result);
        }

        [HttpGet("{id:int}/file")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDocumentFile(int id, [FromQuery] int? versionId = null)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int requestingUserId))
            {
                return Unauthorized(new { message = "Invalid user token." });
            }

            var file = await _documentService.GetDocumentFileAsync(id, requestingUserId, versionId);

            return PhysicalFile(file.FilePath, file.MimeType,
                enableRangeProcessing: true);
        }
    }
}
