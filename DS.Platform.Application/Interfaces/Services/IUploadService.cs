using DS.Platform.Application.DTOs.Files.Request;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace DS.Platform.Application.Interfaces.Services
{
    public interface IUploadService
    {
        // string UploadAsync(UploadRequest request, string folder);
        Task<byte[]> GetFile(string container, string route);
        Task<string> SaveFile(string container, IFormFile file);
        Task<string> EditFile(string container, IFormFile file, string route);
        Task RemoveFile(string route, string container);
    }
}