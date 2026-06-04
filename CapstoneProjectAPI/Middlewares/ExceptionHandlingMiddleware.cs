using System.Net;
using System.Text.Json;
using CapstoneProjectAPI.Exceptions;

namespace CapstoneProjectAPI.Middlewares
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            HttpStatusCode statusCode;
            string errorType;

            switch (ex)
            {
                case EntityNotFoundException:
                    statusCode = HttpStatusCode.NotFound;
                    errorType  = "NotFound";
                    break;

                case UnauthorizedAccessException:
                    statusCode = HttpStatusCode.Unauthorized;
                    errorType  = "Unauthorized";
                    break;

                case InvalidOperationException:
                    statusCode = HttpStatusCode.Conflict;
                    errorType  = "Conflict";
                    break;

                case ArgumentException:
                    statusCode = HttpStatusCode.BadRequest;
                    errorType  = "BadRequest";
                    break;

                case UnableToCreateEntityException:
                    statusCode = HttpStatusCode.BadRequest;
                    errorType  = "BadRequest";
                    break;

                default:
                    statusCode = HttpStatusCode.InternalServerError;
                    errorType  = "InternalServerError";
                    break;
            }

            var response = new
            {
                statusCode = (int)statusCode,
                errorType,
                message = ex.Message
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode  = (int)statusCode;

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return context.Response.WriteAsync(json);
        }
    }
}
