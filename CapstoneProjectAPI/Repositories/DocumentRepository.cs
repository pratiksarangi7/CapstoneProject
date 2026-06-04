using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Models;

namespace CapstoneProjectAPI.Repositories
{
    public class DocumentRepository : Repository<int, Document>
    {
        public DocumentRepository(AppDbContext context) : base(context)
        {
        }
    }
}
