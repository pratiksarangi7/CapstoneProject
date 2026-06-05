using Microsoft.EntityFrameworkCore;
using AutoMapper;
using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Services;
using CapstoneProjectAPI.Mappings;
using CapstoneProjectAPI.Exceptions;

namespace CapstoneProjectTest
{
    [TestFixture]
    public class AdminServiceTests
    {
        private AppDbContext _context;
        private IMapper _mapper;
        private AdminService _adminService;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "CapstoneAdminDb_" + Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);

            var mappingConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

            _mapper = mappingConfig.CreateMapper();
            _adminService = new AdminService(_context, _mapper);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task GetUsers_ReturnsPagedUsers_Successfully()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            for (int i = 1; i <= 15; i++)
            {
                _context.Users.Add(new User
                {
                    Name = $"User {i}",
                    Email = $"user{i}@test.com",
                    PasswordHash = "hashed",
                    DepartmentId = dept.Id
                });
            }
            await _context.SaveChangesAsync();

            var result = await _adminService.GetUsers(pageNumber: 2, pageSize: 5);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Items.Count, Is.EqualTo(5));
            Assert.That(result.TotalCount, Is.EqualTo(15));
            Assert.That(result.PageNumber, Is.EqualTo(2));
            Assert.That(result.PageSize, Is.EqualTo(5));
            Assert.That(result.TotalPages, Is.EqualTo(3));
            Assert.That(result.Items.First().Name, Is.EqualTo("User 6"));
        }

        [Test]
        public async Task GetUsers_CoercesInvalidParameters()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            _context.Users.Add(new User { Name = "User 1", Email = "user1@test.com", PasswordHash = "hashed", DepartmentId = dept.Id });
            await _context.SaveChangesAsync();

            var result = await _adminService.GetUsers(pageNumber: -1, pageSize: 200);

            Assert.That(result.PageNumber, Is.EqualTo(1));
            Assert.That(result.PageSize, Is.EqualTo(10));
        }

        [Test]
        public async Task ChangeUserManager_ValidRequest_UpdatesManagerSuccessfully()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            var manager = new User { Name = "Manager", Email = "mgr@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            _context.Users.AddRange(user, manager);
            await _context.SaveChangesAsync();

            var request = new ChangeUserManagerRequestDto
            {
                UserId = user.Id,
                ManagerId = manager.Id
            };

            var result = await _adminService.ChangeUserManager(request);
            Assert.That(result, Is.True);
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.That(updatedUser, Is.Not.Null);
            Assert.That(updatedUser!.ManagerId, Is.EqualTo(manager.Id));
        }

        [Test]
        public async Task ChangeUserManager_NullManagerId_ClearsManager()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var manager = new User { Name = "Manager", Email = "mgr@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            _context.Users.Add(manager);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = dept.Id, ManagerId = manager.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ChangeUserManagerRequestDto
            {
                UserId = user.Id,
                ManagerId = null
            };

            var result = await _adminService.ChangeUserManager(request);

            Assert.That(result, Is.True);
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.That(updatedUser, Is.Not.Null);
            Assert.That(updatedUser!.ManagerId, Is.Null);
        }

        [Test]
        public void ChangeUserManager_UserNotFound_ThrowsEntityNotFoundException()
        {
            var request = new ChangeUserManagerRequestDto
            {
                UserId = 999,
                ManagerId = 1
            };

            Assert.ThrowsAsync<EntityNotFoundException>(async () => await _adminService.ChangeUserManager(request));
        }

        [Test]
        public async Task ChangeUserManager_ManagerNotFound_ThrowsEntityNotFoundException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ChangeUserManagerRequestDto
            {
                UserId = user.Id,
                ManagerId = 999
            };
            Assert.ThrowsAsync<EntityNotFoundException>(async () => await _adminService.ChangeUserManager(request));
        }

        [Test]
        public async Task ChangeUserManager_SelfAsManager_ThrowsArgumentException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ChangeUserManagerRequestDto
            {
                UserId = user.Id,
                ManagerId = user.Id
            };

            Assert.ThrowsAsync<ArgumentException>(async () => await _adminService.ChangeUserManager(request));
        }

        [Test]
        public async Task ChangeUserLevel_ValidRequest_UpdatesLevelSuccessfully()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = dept.Id, Level = 1 };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ChangeUserLevelRequestDto
            {
                UserId = user.Id,
                Level = 5
            };

            var result = await _adminService.ChangeUserLevel(request);

            Assert.That(result, Is.True);
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.That(updatedUser, Is.Not.Null);
            Assert.That(updatedUser!.Level, Is.EqualTo(5));
        }

        [Test]
        public void ChangeUserLevel_LevelLessThanOne_ThrowsArgumentException()
        {
            var request = new ChangeUserLevelRequestDto
            {
                UserId = 1,
                Level = 0
            };

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(async () => await _adminService.ChangeUserLevel(request));
        }

        [Test]
        public void ChangeUserLevel_UserNotFound_ThrowsEntityNotFoundException()
        {
            var request = new ChangeUserLevelRequestDto
            {
                UserId = 999,
                Level = 2
            };

            Assert.ThrowsAsync<EntityNotFoundException>(async () => await _adminService.ChangeUserLevel(request));
        }

        [Test]
        public async Task ChangeUserDepartment_ValidRequest_UpdatesDepartmentSuccessfully()
        {
            var deptA = new Department { Name = "HR" };
            var deptB = new Department { Name = "Engineering" };
            _context.Departments.AddRange(deptA, deptB);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = deptA.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ChangeUserDepartmentRequestDto
            {
                UserId = user.Id,
                DepartmentId = deptB.Id
            };

            var result = await _adminService.ChangeUserDepartment(request);
            Assert.That(result, Is.True);
            var updatedUser = await _context.Users.FindAsync(user.Id);
            Assert.That(updatedUser, Is.Not.Null);
            Assert.That(updatedUser!.DepartmentId, Is.EqualTo(deptB.Id));
        }

        [Test]
        public void ChangeUserDepartment_UserNotFound_ThrowsEntityNotFoundException()
        {
            var request = new ChangeUserDepartmentRequestDto
            {
                UserId = 999,
                DepartmentId = 1
            };
            Assert.ThrowsAsync<EntityNotFoundException>(async () => await _adminService.ChangeUserDepartment(request));
        }

        [Test]
        public async Task ChangeUserDepartment_DepartmentNotFound_ThrowsEntityNotFoundException()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();

            var user = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ChangeUserDepartmentRequestDto
            {
                UserId = user.Id,
                DepartmentId = 999
            };
            Assert.ThrowsAsync<EntityNotFoundException>(async () => await _adminService.ChangeUserDepartment(request));
        }

        [Test]
        public async Task AddDepartment_ValidRequest_AddsAndReturnsDepartment()
        {
            var request = new AddDepartmentRequestDto
            {
                Name = "Finance"
            };
            var result = await _adminService.AddDepartment(request);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.GreaterThan(0));
            Assert.That(result.Name, Is.EqualTo("Finance"));

            var inDb = await _context.Departments.AnyAsync(d => d.Name == "Finance");
            Assert.That(inDb, Is.True);
        }

        [Test]
        public void AddDepartment_EmptyName_ThrowsArgumentException()
        {
            var request = new AddDepartmentRequestDto { Name = "   " };
            Assert.ThrowsAsync<ArgumentException>(async () => await _adminService.AddDepartment(request));
        }

        [Test]
        public async Task AddDepartment_DuplicateName_ThrowsInvalidOperationException()
        {
            _context.Departments.Add(new Department { Name = "Marketing" });
            await _context.SaveChangesAsync();
            var request = new AddDepartmentRequestDto { Name = "marketing" };
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _adminService.AddDepartment(request));
        }

        [Test]
        public async Task GetDepartments_ReturnsDepartmentsWithUsersAndManagers()
        {
            var dept = new Department { Name = "HR" };
            _context.Departments.Add(dept);
            await _context.SaveChangesAsync();
            var manager = new User { Name = "Manager", Email = "mgr@test.com", PasswordHash = "hashed", DepartmentId = dept.Id };
            _context.Users.Add(manager);
            await _context.SaveChangesAsync();
            var employee = new User { Name = "Employee", Email = "emp@test.com", PasswordHash = "hashed", DepartmentId = dept.Id, ManagerId = manager.Id };
            _context.Users.Add(employee);
            await _context.SaveChangesAsync();
            var result = await _adminService.GetDepartments();
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.GreaterThanOrEqualTo(1));
            var hrDept = result.FirstOrDefault(d => d.Name == "HR");
            Assert.That(hrDept, Is.Not.Null);
            Assert.That(hrDept.Users.Count, Is.EqualTo(2));
            var empDto = hrDept.Users.FirstOrDefault(u => u.Name == "Employee");
            Assert.That(empDto, Is.Not.Null);
            Assert.That(empDto.ManagerName, Is.EqualTo("Manager"));
        }
    }
}