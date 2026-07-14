using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CapstoneProjectAPI.Data
{
    /// <summary>
    /// Design-time factory used exclusively by EF Core tooling (e.g., `dotnet ef migrations add`).
    /// It bypasses the full Program.cs startup (and Azure Key Vault) by building a minimal
    /// configuration from appsettings files and environment variables only.
    /// This class is never invoked at runtime.
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
