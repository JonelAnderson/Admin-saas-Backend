
using DS.Platform.Application.DTOs.Files.Request;
using DS.Platform.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DS.Platform.Infrastructure.Services
{
    public class FileStorageLocal : IFileStorageLocal
    {
        public async Task<byte[]> GetFile(string container, string route)
        {
            try
            {
                if (string.IsNullOrEmpty(route))
                {
                    return null!;
                }

                var fileName = Path.GetFileName(route);

                var directoryFile = Path.Combine("wwwroot", container, fileName);

                if (!File.Exists(directoryFile))
                {
                    return null!;
                }

                using (var stream = new FileStream(directoryFile, FileMode.Open, FileAccess.Read))
                {
                    var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    return memoryStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
        public async Task<string> SaveFile(string container, IFormFile file,  string scheme, string host)
        {

            try
            {
                if (file == null || file.Length == 0) return string.Empty;

                // Nunca el nombre del cliente: dos archivos "foto.jpg" se pisarían y un nombre con rutas podría salir de la carpeta.
                var extension = Path.GetExtension(Path.GetFileName(file.FileName ?? string.Empty)).ToLowerInvariant();
                if (extension.Length > 10 || extension.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '.'))
                {
                    extension = string.Empty;
                }

                var fileName = $"{Guid.NewGuid():N}{extension}";

                string folder = Path.Combine("wwwroot", container);

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }


                var currentUrl = $"{scheme}://{host}";
                var pathDb = Path.Combine(currentUrl, container, fileName).Replace("\\", "/");
                return pathDb;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
           
        }

        public async Task<string> EditFile(string container, IFormFile file, string route, string scheme, string host)
        {
            await RemoveFile(route, container);

            return await SaveFile(container, file, scheme, host);
        }

        public Task RemoveFile(string route, string container)
        {
            if (string.IsNullOrEmpty(route))
                return Task.CompletedTask;

            var fileName = Path.GetFileName(route);

            var directoryFile = Path.Combine("wwwroot", container, fileName);

            if (File.Exists(directoryFile))
                File.Delete(directoryFile);

            return Task.CompletedTask;
        }
    }
}
