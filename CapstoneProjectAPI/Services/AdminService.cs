using AutoMapper;
using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Exceptions;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CapstoneProjectAPI.Services
{
    public class AdminService
    {
        public AppDbContext _context;
        private readonly IMapper _mapper;

        public AdminService(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        public async Task<List<UserDetailsResponseDto>> GetUsers()
        {
            var users = await _context.Users
                .Include(u => u.Department)
                .Include(u => u.Manager)
                .ToListAsync();

            return _mapper.Map<List<UserDetailsResponseDto>>(users);
        }

        public async Task<bool> ChangeUserManager(ChangeUserManagerRequestDto request)
        {
            var user = await _context.Users.FindAsync(request.UserId)
                ?? throw new EntityNotFoundException("User not found");

            if (request.ManagerId.HasValue)
            {
                var manager = await _context.Users.FindAsync(request.ManagerId.Value)
                    ?? throw new EntityNotFoundException("Manager not found");

                if (request.ManagerId.Value == request.UserId)
                {
                    throw new ArgumentException("A user cannot be their own manager.");
                }
            }

            user.ManagerId = request.ManagerId;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangeUserLevel(ChangeUserLevelRequestDto request)
        {
            if (request.Level < 1)
            {
                throw new ArgumentException("Level must be a positive integer.");
            }

            var user = await _context.Users.FindAsync(request.UserId)
                ?? throw new EntityNotFoundException("User not found");

            user.Level = request.Level;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangeUserDepartment(ChangeUserDepartmentRequestDto request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
            {
                throw new EntityNotFoundException("User not found");
            }

            var departmentExists = await _context.Departments.AnyAsync(d => d.Id == request.DepartmentId);
            if (!departmentExists)
            {
                throw new EntityNotFoundException("Department not found");
            }

            user.DepartmentId = request.DepartmentId;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Department> AddDepartment(AddDepartmentRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Department name cannot be empty.");
            }

            var departmentExists = await _context.Departments
                .AnyAsync(d => d.Name.ToLower() == request.Name.ToLower());

            if (departmentExists)
            {
                throw new InvalidOperationException($"A department with name '{request.Name}' already exists.");
            }

            var department = new Department
            {
                Name = request.Name
            };

            _context.Departments.Add(department);
            await _context.SaveChangesAsync();

            return department;
        }

        public async Task<List<DepartmentResponseDto>> GetDepartments()
        {
            var departments = await _context.Departments
                .Include(d => d.Users)
                    .ThenInclude(u => u.Manager)
                .Select(d => new DepartmentResponseDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Users = d.Users.Select(u => new DepartmentUserDto
                    {
                        Id = u.Id,
                        Name = u.Name,
                        Email = u.Email,
                        IsAdmin = u.IsAdmin,
                        ManagerId = u.ManagerId,
                        ManagerName = u.Manager != null ? u.Manager.Name : null,
                        Level = u.Level
                    }).ToList()
                })
                .ToListAsync();
            return departments;
        }
    }
}