using DS.Platform.Application.DTOs.Dashboard.Response;
using DS.Platform.Application.Features.Instances.Common;
using DS.Platform.Application.Helpers;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Application.Features.Dashboard.Queries.Get
{
    /// <summary>
    /// Tablero de inicio: clientes por estado de suscripción, cobrado del mes, vencimientos de la semana, instancias sin contacto o
    /// con versión vieja, acciones y avisos fallidos y el resumen de la API SUNAT (certificados por vencer, rechazos del día). Solo
    /// lee. Si la API SUNAT no responde, esa parte llega vacía con el motivo: el tablero no falla por una caída ajena.
    /// </summary>
    public class GetDashboardQuery : IRequest<Result<DashboardResponseDto>>
    {
    }

    internal class GetDashboardQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IApiSunatAdminClient apiSunat,
        IDateTimeService dateTime) : IRequestHandler<GetDashboardQuery, Result<DashboardResponseDto>>
    {
        public const int WeekDays = 7;
        public const int ListLimit = 10;

        public async Task<Result<DashboardResponseDto>> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
        {
            var now = dateTime.NowUtc;
            var today = LimaDate.Today(now);
            var weekEnd = today.AddDays(WeekDays - 1);
            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var nextMonth = monthStart.AddMonths(1);

            var dto = new DashboardResponseDto
            {
                Today = today.ToString("yyyy-MM-dd"),
                Customers = await unitOfWork.Customers.GetAllQueryable().CountAsync(cancellationToken)
            };

            // Suscripciones por estado (todas las no borradas, también las canceladas).
            var byStatus = await unitOfWork.Subscriptions.GetAllQueryable().AsNoTracking()
                .GroupBy(s => s.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
            foreach (var status in new[] { SubscriptionStatus.Trial, SubscriptionStatus.Active, SubscriptionStatus.Grace, SubscriptionStatus.Suspended, SubscriptionStatus.Cancelled })
            {
                dto.SubscriptionsByStatus.Add(new DashboardCountDto
                {
                    Status = (int)status, Name = StatusName(status), Count = byStatus.FirstOrDefault(x => x.Status == status)?.Count ?? 0
                });
            }

            // Cobrado del mes por moneda.
            var registered = PaymentStatus.Registered;
            dto.MonthRevenue = await unitOfWork.Payments.GetAllQueryable().AsNoTracking()
                .Where(p => p.Status == registered && p.PaidOn >= monthStart && p.PaidOn < nextMonth)
                .GroupBy(p => p.Currency)
                .Select(g => new DashboardRevenueDto { Currency = g.Key, Amount = g.Sum(p => p.Amount), Payments = g.Count() })
                .OrderBy(r => r.Currency).ToListAsync(cancellationToken);

            // Vencimientos de la semana: lo cubierto que vence y la gracia que termina en suspensión.
            var open = await unitOfWork.Subscriptions.GetAllQueryable().AsNoTracking()
                .Where(s => (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active) && s.CoveredUntil >= today && s.CoveredUntil <= weekEnd
                            || s.Status == SubscriptionStatus.Grace && s.SuspendOn != null && s.SuspendOn >= today && s.SuspendOn <= weekEnd)
                .Select(s => new
                {
                    s.Id, s.CustomerId, CustomerName = s.Customer!.BusinessName, s.Product, PlanName = s.Plan!.Name,
                    s.Status, s.CoveredUntil, s.SuspendOn
                }).ToListAsync(cancellationToken);
            dto.DueThisWeek = open
                .Select(s =>
                {
                    var grace = s.Status == SubscriptionStatus.Grace;
                    var date = grace ? s.SuspendOn!.Value : s.CoveredUntil;
                    return new DashboardDueDto
                    {
                        SubscriptionId = s.Id, CustomerId = s.CustomerId, CustomerName = s.CustomerName,
                        ProductName = s.Product == PlanProduct.Erp ? "ERP" : "API SUNAT", PlanName = s.PlanName,
                        Event = grace ? "Se suspende" : "Vence", Date = date.ToString("yyyy-MM-dd"), DaysLeft = date.DayNumber - today.DayNumber
                    };
                })
                .OrderBy(d => d.Date).ThenBy(d => d.CustomerName).Take(20).ToList();

            // Instancias del ERP: sin contacto y con versión vieja.
            var instances = await unitOfWork.ErpInstances.GetAllQueryable().AsNoTracking()
                .Select(i => new { i.Id, i.CustomerId, CustomerName = i.Customer!.BusinessName, i.Name, i.Version, i.LastContactOnUtc })
                .ToListAsync(cancellationToken);
            dto.InstancesTotal = instances.Count;
            DashboardInstanceDto ToDto(int id, int customerId, string customer, string name, string? version, DateTime? last) => new()
            {
                Id = id, CustomerId = customerId, CustomerName = customer, Name = name, Version = version, LastContactOnUtc = last,
                ContactName = InstanceContact.Describe(last, now).Name
            };
            dto.InstancesWithoutContact = instances
                .Where(i => i.LastContactOnUtc is null || now - i.LastContactOnUtc.Value > InstanceContact.Delayed)
                .OrderBy(i => i.LastContactOnUtc ?? DateTime.MinValue).Take(ListLimit)
                .Select(i => ToDto(i.Id, i.CustomerId, i.CustomerName, i.Name, i.Version, i.LastContactOnUtc)).ToList();
            var latest = instances.Select(i => ParseVersion(i.Version)).Where(v => v is not null).Max();
            dto.LatestVersion = latest?.ToString();
            dto.InstancesOutdated = latest is null ? [] : instances
                .Where(i => ParseVersion(i.Version) is Version v && v < latest)
                .OrderBy(i => ParseVersion(i.Version)).Take(ListLimit)
                .Select(i => ToDto(i.Id, i.CustomerId, i.CustomerName, i.Name, i.Version, i.LastContactOnUtc)).ToList();

            dto.RemoteActionsNeedingAttention = await unitOfWork.RemoteActions.CountNeedingAttentionAsync(cancellationToken);
            dto.NotificationsNeedingAttention = await unitOfWork.Notifications.CountNeedingAttentionAsync(cancellationToken);
            dto.Api = await ReadApiAsync(today, cancellationToken);

            return await Result<DashboardResponseDto>.SuccessAsync(dto);
        }

        private async Task<DashboardApiDto> ReadApiAsync(DateOnly today, CancellationToken cancellationToken)
        {
            var summary = await apiSunat.GetDashboardAsync(cancellationToken);
            if (!summary.Success)
            {
                return new DashboardApiDto { Available = false, Error = summary.Error };
            }
            var rejections = await apiSunat.CountRejectionsAsync(today, cancellationToken);
            var d = summary.Data!;
            return new DashboardApiDto
            {
                Available = true, TenantsActive = d.TenantsActive, DocumentsThisMonth = d.DocumentsThisMonth, RejectedThisMonth = d.RejectedThisMonth,
                RejectedToday = rejections.Success ? rejections.Data : null, PendingNow = d.PendingNow, FailedJobs = d.FailedJobs,
                CertificatesExpiring = d.CertificatesExpiring, CertificatesBroken = d.CertificatesBroken
            };
        }

        /// <summary>Versión como número comparable ("1.4.0" y "1.4.0+abc" valen lo mismo); null si no se entiende.</summary>
        internal static Version? ParseVersion(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            var core = text.Trim().TrimStart('v', 'V').Split('+', '-')[0];
            return Version.TryParse(core, out var version) ? version : null;
        }

        private static string StatusName(SubscriptionStatus status) => status switch
        {
            SubscriptionStatus.Trial => "En prueba",
            SubscriptionStatus.Active => "Activas",
            SubscriptionStatus.Grace => "En gracia",
            SubscriptionStatus.Suspended => "Suspendidas",
            _ => "Canceladas"
        };
    }
}
