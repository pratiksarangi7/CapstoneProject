using System.Text;
using CapstoneProjectAPI.Middlewares;
using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Interfaces;
using CapstoneProjectAPI.Mappings;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Repositories;
using CapstoneProjectAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Threading.RateLimiting;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

#region Rate Limiting
builder.Services.AddRateLimiter(
    options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Too many requests.",
            message = "You have exceeded your allowed requests. Please try again later."
        }, cancellationToken: token);
    };
        options.AddPolicy("FixedPerUser", httpContext =>
            {
                string partitionKey;

                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    var userIdClaim = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

                    partitionKey = $"user-{userIdClaim ?? "unknown-id"}";
                }
                else
                {
                    var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
                    partitionKey = $"ip-{clientIp}";
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });
    }
);
#endregion

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token. The 'Bearer ' prefix is added automatically by Swagger UI."
    });

    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = []
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

#region JWT
var jwtSection = builder.Configuration.GetSection("JWT");
var key = Encoding.UTF8.GetBytes(jwtSection["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
    };
});
#endregion
builder.Services.AddAuthorization();

#region DI Register
builder.Services.AddScoped<IRepository<int, User>, UserRepository>();
builder.Services.AddScoped<IRepository<int, Department>, DepartmentRepository>();
builder.Services.AddScoped<IRepository<int, Document>, DocumentRepository>();
builder.Services.AddScoped<IRepository<int, DocumentVersion>, DocumentVersionRepository>();
builder.Services.AddScoped<IRepository<int, ApprovalAction>, ApprovalActionRepository>();
builder.Services.AddScoped<IRepository<int, AuditLog>, AuditLogRepository>();

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddAutoMapper(m => m.AddProfile(new MappingProfile()));
#endregion

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CapstoneProject API v1"));
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();
