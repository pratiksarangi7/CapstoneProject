using Moq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CapstoneProjectAPI.Data;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;
using CapstoneProjectAPI.Models.Enums;
using CapstoneProjectAPI.Services;
using CapstoneProjectAPI.Exceptions;

namespace CapstoneProjectTest
{
    [TestFixture]
    public partial class DocumentServiceTests
    {
        private AppDbContext _context;
        private Mock<IWebHostEnvironment> _mockEnv;
        private DocumentService _documentService;
        private string _testUploadsFolder;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "CapstoneDocDb_" + Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);

            _testUploadsFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Uploads");

            _mockEnv = new Mock<IWebHostEnvironment>();
            _mockEnv.Setup(e => e.ContentRootPath).Returns(TestContext.CurrentContext.TestDirectory);

            var mockLogger = new Mock<ILogger<DocumentService>>();
            _documentService = new DocumentService(_context, _mockEnv.Object, mockLogger.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();

            if (Directory.Exists(_testUploadsFolder))
            {
                try
                {
                    Directory.Delete(_testUploadsFolder, recursive: true);
                }
                catch { }
            }
        }

        private Mock<IFormFile> CreateMockFile(string fileName, string contentType, long length)
        {
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.ContentType).Returns(contentType);
            mockFile.Setup(f => f.Length).Returns(length);
            mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<Stream, CancellationToken>((stream, token) =>
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes("Dummy file content");
                    stream.Write(bytes, 0, bytes.Length);
                })
                .Returns(Task.CompletedTask);
            return mockFile;
        }

    }
}