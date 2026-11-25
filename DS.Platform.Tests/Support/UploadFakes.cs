using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Shared.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Tests.Support
{
    /// <summary>Almacenamiento de archivos en memoria: registra las llamadas y devuelve URLs "https://erp.test/{carpeta}/{n}.ext".</summary>
    public sealed class FakeUploadService : IUploadService
    {
        private int _next;
        public List<string> Calls { get; } = [];

        public Task<byte[]> GetFile(string container, string route) => Task.FromResult(Array.Empty<byte>());

        public Task<string> SaveFile(string container, IFormFile file)
        {
            Calls.Add($"save {container}");
            return Task.FromResult(Url(container, file));
        }

        public Task<string> EditFile(string container, IFormFile file, string route)
        {
            Calls.Add($"edit {container} {route}");
            return Task.FromResult(Url(container, file));
        }

        public Task RemoveFile(string route, string container)
        {
            Calls.Add($"remove {container} {route}");
            return Task.CompletedTask;
        }

        private string Url(string container, IFormFile file)
            => $"https://erp.test/{container}/{++_next}{Path.GetExtension(file.FileName).ToLowerInvariant()}";

        public static IFormFile File(string name, byte[] content) => new FormFile(new MemoryStream(content), 0, content.Length, "file", name);

        /// <summary>Cabecera PNG válida (8 bytes de firma + IHDR).</summary>
        public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];
    }

    /// <summary>Localizador que devuelve la clave.</summary>
    public sealed class TestLocalizer : IStringLocalizer<ServerCommonResources>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
