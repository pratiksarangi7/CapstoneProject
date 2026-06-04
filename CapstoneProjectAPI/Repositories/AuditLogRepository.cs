using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Models;

namespace CapstoneProjectAPI.Repositories
{
    public class AuditLogRepository : Repository<int, AuditLog>
    {
        public AuditLogRepository(AppDbContext context) : base(context)
        {
        }
    }
}
