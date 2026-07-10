using System.IO;
using System.Threading.Tasks;

namespace CapstoneProjectAPI.Interfaces
{
    public interface IBlobStorageService
    {
        Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
        Task<Stream> DownloadFileAsync(string blobName);
        Task DeleteFileAsync(string blobName);
    }
}
