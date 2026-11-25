using System.Linq;
using DS.Platform.Application.Mappings.Audits;
using System.Globalization;
using DS.Platform.Application.DTOs.Audits.Request;
using DS.Platform.Application.DTOs.Audits.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Shared.Wrapper;
using MediatR;

namespace DS.Platform.Application.Features.Audits.Queries.GetAuditTrails
{
    /// <summary>
    /// Pista de auditoría completa (todas las empresas y usuarios), paginada y filtrada. Solo SuperAdmin: la pista no es de
    /// una empresa, así que ni el Administrador de una empresa la ve (aunque su rol tenga el permiso de auditoría).
    /// </summary>
    public class GetAuditTrailsQuery : IRequest<Result<IEnumerable<AuditTrailResponseDto>>>
    {
        public AuditTrailFiltersRequest? Filters { get; set; }
    }

    internal class GetAuditTrailsQueryHandler(
        IUnitOfWork<int> unitOfWork,
        ICurrentUserService currentUserService) : IRequestHandler<GetAuditTrailsQuery, Result<IEnumerable<AuditTrailResponseDto>>>
    {
        public const string OnlyPlatformAdminMessage = "Solo el SuperAdmin puede consultar la auditoría.";
        public const string InvalidDateMessage = "Las fechas deben tener el formato AAAA-MM-DD.";
        public const string DateOrderMessage = "La fecha inicial no puede ser posterior a la final.";

        public async Task<Result<IEnumerable<AuditTrailResponseDto>>> Handle(GetAuditTrailsQuery request, CancellationToken cancellationToken)
        {
            if (!currentUserService.IsPlatformAdmin)
                return await Result<IEnumerable<AuditTrailResponseDto>>.FailAsync(OnlyPlatformAdminMessage);

            var filters = request.Filters ?? new AuditTrailFiltersRequest();
            // Una pantalla, no una exportación: la pista puede tener millones de filas, siempre de a una página.
            filters.Download = false;

            if (!TryParseDay(filters.StartDate, out var from) || !TryParseDay(filters.EndDate, out var to))
                return await Result<IEnumerable<AuditTrailResponseDto>>.FailAsync(InvalidDateMessage);
            if (from is DateTime f && to is DateTime t && f > t)
                return await Result<IEnumerable<AuditTrailResponseDto>>.FailAsync(DateOrderMessage);

            // La columna guarda la hora de Lima marcada como UTC: los límites del día se comparan igual (hasta el día siguiente, sin incluirlo).
            var (items, total) = await unitOfWork.AuditTrails.GetPaginatedAsync(filters, from, to?.AddDays(1), cancellationToken);

            var rows = items.Select(x => x.ToTrailDto()).ToList();
            var userIds = rows.Where(r => !string.IsNullOrEmpty(r.UserId)).Select(r => r.UserId!).Distinct().ToList();
            var names = await unitOfWork.AuditTrails.UserNamesAsync(userIds, cancellationToken);
            foreach (var row in rows)
            {
                row.UserName = row.UserId is null ? null : names.GetValueOrDefault(row.UserId, row.UserId);
            }

            return await Result<IEnumerable<AuditTrailResponseDto>>.SuccessAsync(rows, rows.Count == 0 ? "Sin resultados." : "Consulta exitosa.", total);
        }

        /// <summary>yyyy-MM-dd → ese día a las 00:00, con Kind UTC como la columna; vacío → sin límite.</summary>
        private static bool TryParseDay(string? value, out DateTime? day)
        {
            day = null;
            if (string.IsNullOrWhiteSpace(value))
                return true;
            if (!DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return false;
            day = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }
    }
}
