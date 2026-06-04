using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Models;

namespace CapstoneProjectAPI.Repositories
{
    public class ApprovalActionRepository : Repository<int, ApprovalAction>
    {
        public ApprovalActionRepository(AppDbContext context) : base(context)
        {
        }
    }
}
