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

var builder = WebApplication.CreateBuilder(args);

// ── MVC Controllers ────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── Swagger / OpenAPI ──────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Define the JWT Bearer security scheme
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token. The 'Bearer ' prefix is added automatically by Swagger UI."
    });

    // Apply the scheme globally – Swashbuckle v10 / Microsoft.OpenApi v2+ syntax
    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = []
    });
});

// ── Database ───────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── JWT Authentication ─────────────────────────────────────────────────────────
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

builder.Services.AddAuthorization();

// ── Repositories ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<IRepository<int, User>, UserRepository>();
builder.Services.AddScoped<IRepository<int, Department>, DepartmentRepository>();
builder.Services.AddScoped<IRepository<int, Document>, DocumentRepository>();
builder.Services.AddScoped<IRepository<int, DocumentVersion>, DocumentVersionRepository>();
builder.Services.AddScoped<IRepository<int, ApprovalAction>, ApprovalActionRepository>();
builder.Services.AddScoped<IRepository<int, AuditLog>, AuditLogRepository>();

// ── Services ───────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddAutoMapper(m=> m.AddProfile(new MappingProfile()));

var app = builder.Build();

// ── Middleware pipeline ────────────────────────────────────────────────────────
// Global exception handler must be registered first so it wraps every other middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CapstoneProject API v1"));
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
