using DS.Platform.Application.DTOs.Files.Request;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace DS.Platform.Application.Interfaces.Services
{
    public interface IFileStorageLocal
    {
        Task<byte[]> GetFile(string container, string route);
        Task<string> SaveFile(string container, IFormFile file, string scheme, string host);
        Task<string> EditFile(string container, IFormFile file, string route, string scheme, string host);
        Task RemoveFile(string route, string container);
    }
}
