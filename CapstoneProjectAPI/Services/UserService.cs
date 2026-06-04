using AutoMapper;
using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Exceptions;
using CapstoneProjectAPI.Interfaces;
using CapstoneProjectAPI.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CapstoneProjectAPI.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;

        public UserService(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<UserDetailsResponseDto> GetUserDetailsAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Department)
                .Include(u => u.Manager)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                throw new EntityNotFoundException($"User with ID {userId} was not found.");
            }

            return _mapper.Map<UserDetailsResponseDto>(user);
        }
    }
}