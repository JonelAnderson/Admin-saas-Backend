using DS.Platform.Application.Configurations;
using DS.Platform.Application.DTOs.Mail.Request;
using DS.Platform.Application.Interfaces.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Threading.Tasks;

namespace DS.Platform.Infrastructure.Shared.Services
{
    public class SMTPMailService : IMailService
    {
        private readonly MailConfiguration _config;
        private readonly ILogger<SMTPMailService> _logger;

        public SMTPMailService(IOptions<MailConfiguration> config, ILogger<SMTPMailService> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        /// <summary>Versión de texto del cuerpo HTML: los enlaces quedan como "texto (url)".</summary>
        internal static string PlainText(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<a[^>]*href=['\"]([^'\"]+)['\"][^>]*>(.*?)</a>", "$2 ($1)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"</p>|<br\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", string.Empty);
            return System.Net.WebUtility.HtmlDecode(text).Trim();
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_config.Host) && !string.IsNullOrWhiteSpace(_config.From);

        public async Task SendAsync(MailRequest request)
        {
            if (string.IsNullOrWhiteSpace(_config.Host) || string.IsNullOrWhiteSpace(_config.From))
            {
                // Sin servidor o remitente no hay envío posible: se registra para que se vea en el log (no se oculta).
                _logger.LogWarning("Correo no enviado a {To}: falta configurar MailSettings (EmailFrom / SmtpHost).", request.To);
                return;
            }
            try
            {
                var sender = new MailboxAddress(_config.DisplayName ?? "ERP", request.From ?? _config.From);
                var email = new MimeMessage
                {
                    Sender = sender,
                    Subject = request.Subject,
                    // HTML + texto plano: un correo solo HTML suma puntos de spam en Gmail / Outlook.
                    Body = new BodyBuilder
                    {
                        HtmlBody = request.Body,
                        TextBody = PlainText(request.Body)
                    }.ToMessageBody()
                };
                // El remitente visible (From): sin él muchos servidores SMTP rechazan el mensaje.
                email.From.Add(sender);
                email.To.Add(MailboxAddress.Parse(request.To));
                using var smtp = new SmtpClient();
                // 465 = SSL directo; 587 / 25 = STARTTLS si el servidor lo ofrece.
                await smtp.ConnectAsync(_config.Host, _config.Port, _config.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable);
                if (!string.IsNullOrWhiteSpace(_config.UserName))
                    await smtp.AuthenticateAsync(_config.UserName, _config.Password);
                var reply = await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
                // El servidor SMTP aceptó el mensaje: si no llega, está en spam o lo frena el proveedor (SPF / remitente no autorizado).
                _logger.LogInformation("Correo \"{Subject}\" aceptado por {Host}:{Port} desde {From}. Respuesta: {Reply}", request.Subject, _config.Host, _config.Port, sender.Address, reply);
            }
            catch (System.Exception ex)
            {
                // Se registra y se relanza: Hangfire marca el trabajo como fallido y lo reintenta (antes se perdía en silencio).
                _logger.LogError(ex, "No se pudo enviar el correo a {To} por {Host}:{Port}.", request.To, _config.Host, _config.Port);
                throw;
            }
        }
    }
}