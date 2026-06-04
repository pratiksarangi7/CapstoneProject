using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CapstoneProjectAPI.Repositories
{
    public class UserRepository : Repository<int, User>
    {
        public UserRepository(AppDbContext context) : base(context)
        {

        }
    }
}