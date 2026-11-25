using DS.Platform.Application.Features.Notifications.Common;
using DS.Platform.Application.Helpers;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Application.Features.Notifications.Commands.QueueCertificateNotices
{
    /// <summary>
    /// Pregunta a la API SUNAT qué certificados digitales están por vencer o vencieron y encola un aviso para el cliente dueño de cada
    /// empresa vinculada (diseño §5.4). Lo envía el job diario <c>CertificateNoticesDaily</c>. Se avisa al entrar en cada tramo (30, 7
    /// y 1 día) y una vez al vencer: la clave lleva la fecha de vencimiento y el tramo, así que renovar el certificado reinicia los
    /// avisos. Solo escribe en la base (el correo sale por <c>SendNotificationsCommand</c>). Devuelve cuántos avisos encoló.
    /// </summary>
    public class QueueCertificateNoticesCommand : IRequest<Result<int>>
    {
        /// <summary>Se consulta a la API los que vencen dentro de estos días (el primer tramo es 30).</summary>
        public int Days { get; set; } = 30;
    }

    internal class QueueCertificateNoticesCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IApiSunatAdminClient apiSunat,
        IDateTimeService dateTime) : IRequestHandler<QueueCertificateNoticesCommand, Result<int>>
    {
        public static string ApiUnavailableMessage(string? error) => $"No se pudo consultar los certificados en la API SUNAT: {error}";

        public async Task<Result<int>> Handle(QueueCertificateNoticesCommand request, CancellationToken cancellationToken)
        {
            var certificates = new List<ApiCertificateInfo>();
            foreach (var status in new[] { "POR_VENCER", "VENCIDO" })
            {
                var call = await apiSunat.GetCertificatesAsync(status, Math.Clamp(request.Days, 1, 365), cancellationToken);
                if (!call.Success)
                {
                    return await Result<int>.FailAsync(ApiUnavailableMessage(call.Error));
                }
                certificates.AddRange(call.Data!);
            }
            certificates = certificates.Where(c => c.ExpiresOn is not null && c.DaysLeft is not null).ToList();
            if (certificates.Count == 0)
            {
                return await Result<int>.SuccessAsync(0, "No hay certificados por vencer.");
            }

            var issuers = await unitOfWork.ApiSunatIssuers.GetByTenantIdsAsync(certificates.Select(c => c.TenantId).Distinct().ToList(), cancellationToken);
            var customerIds = issuers.Select(i => i.CustomerId).Distinct().ToList();
            var customers = await unitOfWork.Customers.GetAllQueryable().Where(c => customerIds.Contains(c.Id)).ToListAsync(cancellationToken);

            var specs = new List<NoticeSpec>();
            foreach (var certificate in certificates)
            {
                var bucket = NoticeRules.CertificateBucket(certificate.DaysLeft!.Value);
                var issuer = issuers.FirstOrDefault(i => i.TenantId == certificate.TenantId);
                var customer = issuer is null ? null : customers.FirstOrDefault(c => c.Id == issuer.CustomerId);
                if (bucket is null || customer is null)
                {
                    continue;
                }
                var (subject, body) = NoticeTemplates.CertificateEmail(new CertificateNotice(
                    customer.BusinessName, certificate.Ruc, certificate.BusinessName, certificate.ExpiresOn, certificate.DaysLeft.Value));
                specs.Add(new NoticeSpec(NotificationType.CertificateExpiring, customer,
                    NoticeRules.CertificateKey(certificate.TenantId, certificate.ExpiresOn!.Value, bucket.Value), subject, body,
                    TenantId: certificate.TenantId));
            }

            var queued = await NotificationOutbox.EnqueueAsync(unitOfWork, specs, dateTime.NowUtc, cancellationToken);
            if (queued > 0)
            {
                await unitOfWork.Commit(cancellationToken);
            }
            return await Result<int>.SuccessAsync(queued, $"{queued} avisos de certificado encolados.");
        }
    }
}
