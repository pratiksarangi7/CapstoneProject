using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Models;

namespace CapstoneProjectAPI.Repositories
{
    public class DepartmentRepository : Repository<int, Department>
    {
        public DepartmentRepository(AppDbContext context) : base(context)
        {
        }
    }
}
