using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Models;

namespace CapstoneProjectAPI.Repositories
{
    public class DocumentVersionRepository : Repository<int, DocumentVersion>
    {
        public DocumentVersionRepository(AppDbContext context) : base(context)
        {
        }
    }
}
