using DS.Platform.Application.DTOs.Mail.Request;
using System.Threading.Tasks;

namespace DS.Platform.Application.Interfaces.Services
{
    public interface IMailService
    {
        /// <summary>Hay servidor y remitente configurados. Sin ellos <see cref="SendAsync"/> no envía (solo lo registra en el log).</summary>
        bool IsConfigured { get; }

        Task SendAsync(MailRequest request);
    }
}