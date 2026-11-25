using DS.Platform.Shared.Wrapper;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace DS.Platform.Application.Validators.Features.Files
{
    public interface IFileUploadValid
    {
        bool IsFileTypeValid(IFormFile file);
        bool IsFileTypeImageValid(IFormFile file);

        bool IsFileImageEnterpriseValid(IFormFile file);
        bool IsFileNameValid(IFormFile file);
        bool IsFileSizeValid(IFormFile file);
        bool IsFileCSVTypeValid(IFormFile file);
        bool IsFileCSVSizeValid(IFormFile file);
        bool IsFileTypeDocsValid(IFormFile file);
        Task<Result<int>> ValidationUrlImagen(IFormFile file);
        Task<Result<int>> ValidationDocsFile(IFormFile file);


    }
}
