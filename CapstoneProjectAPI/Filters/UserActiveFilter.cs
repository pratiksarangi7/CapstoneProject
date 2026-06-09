using System.Security.Claims;
using CapstoneProjectAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CapstoneProjectAPI.Filters
{
    public class UserActiveFilter : IAsyncActionFilter
    {
        private readonly AppDbContext _context;

        public UserActiveFilter(AppDbContext context)
        {
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userIdClaim = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null || !user.IsActive)
                {
                    context.Result = new UnauthorizedObjectResult(new { message = "Your account has been deactivated. Please contact your administrator." });
                    return;
                }
            }
            await next();
        }
    }
}
