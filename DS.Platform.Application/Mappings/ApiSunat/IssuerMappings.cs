using System.Linq.Expressions;
using DS.Platform.Application.DTOs.ApiSunat.Response;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Domain.Entities.ApiSunat;

namespace DS.Platform.Application.Mappings.ApiSunat
{
    /// <summary>
    /// Mapeo explícito de emisores (sin AutoMapper): proyección que EF traduce a SQL (el cliente sale por JOIN) y el paso de lo que
    /// informa la API SUNAT a la entidad. La empresa, el cliente y el estado de suspensión los pone el handler, nunca el formulario.
    /// </summary>
    public static class IssuerMappings
    {
        public static readonly Expression<Func<ApiSunatIssuer, IssuerResponseDto>> ToResponse = i => new IssuerResponseDto
        {
            Id = i.Id,
            CustomerId = i.CustomerId,
            CustomerRuc = i.Customer != null ? i.Customer.Ruc : string.Empty,
            CustomerName = i.Customer != null ? i.Customer.BusinessName : string.Empty,
            TenantId = i.TenantId,
            Ruc = i.Ruc,
            BusinessName = i.BusinessName,
            TradeName = i.TradeName,
            Environment = i.Environment,
            ApiPlan = i.ApiPlan,
            DocumentsThisMonth = i.DocumentsThisMonth,
            MaxDocumentsMonth = i.MaxDocumentsMonth,
            SuspendDesired = i.SuspendDesired,
            SuspendApplied = i.SuspendApplied,
            SyncPending = i.SuspendDesired != i.SuspendApplied,
            DesiredPlan = i.PlanDesiredSlug,
            PlanPending = i.PlanDesiredSlug != null
                && (i.PlanDesiredSlug != i.ApiPlan || (i.PlanDesiredMaxDocuments != null && i.PlanDesiredMaxDocuments != i.MaxDocumentsMonth)),
            LastSyncedOnUtc = i.LastSyncedOnUtc,
            Notes = i.Notes
        };

        /// <summary>Emisor nuevo con lo que informa la API; si la API ya la tiene suspendida por pago, nace con ese estado aplicado.</summary>
        public static ApiSunatIssuer ToEntity(this ApiTenantInfo tenant, int customerId, string? notes, DateTime nowUtc)
        {
            var issuer = new ApiSunatIssuer
            {
                CustomerId = customerId,
                TenantId = tenant.Id,
                Ruc = tenant.Ruc,
                BusinessName = tenant.BusinessName,
                IsActive = true,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            };
            tenant.ApplyTo(issuer, nowUtc);
            issuer.SuspendDesired = tenant.BillingSuspended;
            issuer.SuspendApplied = tenant.BillingSuspended;
            return issuer;
        }

        /// <summary>Copia lo que informa la API (datos y uso); no toca el estado de suspensión que decide la consola.</summary>
        public static void ApplyTo(this ApiTenantInfo tenant, ApiSunatIssuer issuer, DateTime nowUtc)
        {
            issuer.Ruc = tenant.Ruc;
            issuer.BusinessName = tenant.BusinessName;
            issuer.TradeName = tenant.TradeName;
            issuer.Environment = tenant.Environment;
            issuer.ApiPlan = tenant.Plan;
            issuer.DocumentsThisMonth = tenant.DocumentsThisMonth;
            issuer.MaxDocumentsMonth = tenant.MaxDocumentsMonth;
            issuer.LastSyncedOnUtc = nowUtc;
        }

        public static ApiTenantResponseDto ToDto(this ApiTenantInfo tenant, bool linked) => new()
        {
            Id = tenant.Id,
            Ruc = tenant.Ruc,
            BusinessName = tenant.BusinessName,
            TradeName = tenant.TradeName,
            Environment = tenant.Environment,
            Plan = tenant.Plan,
            IsActive = tenant.IsActive,
            BillingSuspended = tenant.BillingSuspended,
            DocumentsThisMonth = tenant.DocumentsThisMonth,
            MaxDocumentsMonth = tenant.MaxDocumentsMonth,
            Linked = linked
        };
    }
}
