using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DS.Platform.Application.Validators.Features.Files
{
    public class FileUploadValid : IFileUploadValid
    {
        private const int MaxFileSizeInBytes = 5 * 1024 * 1024; // 5 MB

        private const int MaxFileCSVSizeInBytes = 20 * 1024 * 1024; // 20 MB

        private readonly IStringLocalizer<ServerCommonResources> _localizer;

        public FileUploadValid(IStringLocalizer<ServerCommonResources> localizer)
        {
            _localizer = localizer;
        }


        public bool IsFileTypeValid(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            var fileExtension = Path.GetExtension(file.FileName);
            return allowedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
        }


        public bool IsFileTypeImageValid(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName);
            return allowedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
        }

        public bool IsFileTypeDocsValid(IFormFile file)
        {
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
            var fileExtension = Path.GetExtension(file.FileName);
            return allowedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
        }

        public bool IsFileImageEnterpriseValid(IFormFile file)
        {
            var allowedExtensions = ".png";
            var fileExtension = Path.GetExtension(file.FileName);
            return allowedExtensions.Equals(fileExtension!.ToLower());

        }

        public bool IsFileNameValid(IFormFile file)
        {
            var allowedNames = new[] { "logo", "portada", "fondo" };

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.FileName);
            return allowedNames.Contains(fileNameWithoutExtension, StringComparer.OrdinalIgnoreCase);
        }

        public bool IsFileSizeValid(IFormFile file)
        {
            var fileSizeInBytes = file!.Length;
            return fileSizeInBytes <= MaxFileSizeInBytes;
        }


        public bool IsFileCSVTypeValid(IFormFile file)
        {
            var allowedExtensions = ".csv" ;
            var fileExtension = Path.GetExtension(file.FileName);
            return allowedExtensions.Equals(fileExtension!.ToLower());
        }

        public bool IsFileCSVSizeValid(IFormFile file)
        {
            var fileSizeInBytes = file!.Length;
            return fileSizeInBytes <= MaxFileCSVSizeInBytes;
        }


        /// <summary>
        /// Los primeros bytes son de una imagen JPEG, PNG o WEBP (y coinciden con la extensión): un archivo renombrado a .png
        /// no pasa por imagen.
        /// </summary>
        public static bool HasImageSignature(IFormFile file)
        {
            Span<byte> head = stackalloc byte[12];
            int read;
            using (var stream = file.OpenReadStream())
            {
                read = stream.Read(head);
            }
            if (read < 12)
            {
                return false;
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var isJpeg = head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF;
            var isPng = head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47
                        && head[4] == 0x0D && head[5] == 0x0A && head[6] == 0x1A && head[7] == 0x0A;
            var isWebp = head[0] == (byte)'R' && head[1] == (byte)'I' && head[2] == (byte)'F' && head[3] == (byte)'F'
                         && head[8] == (byte)'W' && head[9] == (byte)'E' && head[10] == (byte)'B' && head[11] == (byte)'P';
            return extension switch
            {
                ".jpg" or ".jpeg" => isJpeg,
                ".png" => isPng,
                ".webp" => isWebp,
                _ => false
            };
        }

        public async Task<Result<int>> ValidationUrlImagen(IFormFile file)
        {
            if (!IsFileTypeImageValid(file) || !HasImageSignature(file))
            {
                return await Result<int>.FailAsync($"'{file.FileName}' {_localizer["MessageValidateFileTypeFile"]}  Solo se permiten archivos de los siguientes tipos: jpg, jpeg, png y webp");
            }

            if (!IsFileSizeValid(file))
            {
                double fileSizeInMB = file!.Length / (1024.0 * 1024.0);
                return await Result<int>.FailAsync($"{_localizer["MessageValidateFileSizeFile"]} El tamaño del archivo  '{file.FileName}'  es de {fileSizeInMB:F2} MB. El tamaño máximo permitido es de 5 MB.");
            }
            else return await Result<int>.SuccessAsync();
        }

        public async Task<Result<int>> ValidationDocsFile(IFormFile file)
        {
            if (!IsFileTypeDocsValid(file))
            {
                return await Result<int>.FailAsync($"'{file.FileName}' {_localizer["MessageValidateFileTypeFile"]}  Solo se permiten archivos de los siguientes tipos: pdf, doc, docx, xls y xlsx");
            }

            if (!IsFileSizeValid(file))
            {
                double fileSizeInMB = file!.Length / (1024.0 * 1024.0);
                return await Result<int>.FailAsync($"{_localizer["MessageValidateFileSizeFile"]} El tamaño del archivo  '{file.FileName}'  es de {fileSizeInMB:F2} MB. El tamaño máximo permitido es de 5 MB.");
            }
            else return await Result<int>.SuccessAsync();
        }

    }
}
