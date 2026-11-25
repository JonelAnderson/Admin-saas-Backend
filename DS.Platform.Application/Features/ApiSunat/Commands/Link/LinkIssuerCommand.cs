using DS.Platform.Application.DTOs.ApiSunat.Request;
using DS.Platform.Application.Features.ApiSunat.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Application.Interfaces.Services.ApiSunat;
using DS.Platform.Application.Mappings.ApiSunat;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.ApiSunat.Commands.Link
{
    /// <summary>
    /// Vincula una empresa de la API SUNAT a un cliente. Los datos (RUC, razón social, ambiente, plan) se leen de la API, no del
    /// formulario. Si el cliente ya tiene suscripciones y ninguna vigente, la empresa nace con la suspensión pedida (acción remota
    /// en la bandeja, en el mismo guardado).
    /// </summary>
    public class LinkIssuerCommand : IRequest<Result<int>>
    {
        public LinkIssuerRequestDto? LinkIssuerRequestDto { get; set; }
    }

    internal class LinkIssuerCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IApiSunatAdminClient apiSunat,
        IDateTimeService dateTime,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<LinkIssuerCommand, Result<int>>
    {
        public const string CustomerNotFoundMessage = "El cliente no existe.";
        public const string CustomerInactiveMessage = "El cliente está inactivo: actívelo antes de vincularle una empresa.";
        public const string TenantNotFoundMessage = "No existe esa empresa en la API SUNAT.";
        public const string AlreadyLinkedMessage = "Esa empresa (o su RUC) ya está vinculada a un cliente.";

        public static string IssuerLimitMessage(int limit)
            => $"El plan del cliente permite hasta {limit} empresas emisoras y ya las tiene. Amplíe su plan para vincular otra.";

        public static string ApiUnavailableMessage(string? error)
            => $"No se pudo consultar la API SUNAT: {error}. Inténtelo de nuevo en un momento.";

        public async Task<Result<int>> Handle(LinkIssuerCommand command, CancellationToken cancellationToken)
        {
            var dto = command.LinkIssuerRequestDto!;

            var customer = await unitOfWork.Customers.GetByIdAsync(dto.CustomerId);
            if (customer is null || customer.IsDeleted)
            {
                return await Result<int>.FailAsync(CustomerNotFoundMessage);
            }
            if (!customer.IsActive)
            {
                return await Result<int>.FailAsync(CustomerInactiveMessage);
            }

            var call = await apiSunat.GetTenantAsync(dto.TenantId, cancellationToken);
            if (!call.Success)
            {
                return await Result<int>.FailAsync(call.StatusCode == 404 ? TenantNotFoundMessage : ApiUnavailableMessage(call.Error));
            }
            var tenant = call.Data!;

            if (await unitOfWork.ApiSunatIssuers.TenantOrRucLinkedAsync(tenant.Id, tenant.Ruc, null, cancellationToken))
            {
                return await Result<int>.FailAsync(AlreadyLinkedMessage);
            }

            // Tope de empresas emisoras del plan (el cupo de comprobantes por empresa lo hace cumplir la API).
            var subscriptions = await unitOfWork.Subscriptions.GetByCustomerAsync(customer.Id, cancellationToken);
            if (ApiPlanPolicy.IssuerLimit(subscriptions) is int limit
                && (await unitOfWork.ApiSunatIssuers.GetByCustomerAsync(customer.Id, cancellationToken)).Count >= limit)
            {
                return await Result<int>.FailAsync(IssuerLimitMessage(limit));
            }

            var now = dateTime.NowUtc;
            var issuer = tenant.ToEntity(customer.Id, dto.Notes, now);
            await unitOfWork.ApiSunatIssuers.AddAsync(issuer);

            // Un cliente que ya está sin suscripción vigente no debe poder emitir por la empresa que acaba de vincular.
            await ApiAccessReconciler.ReconcileIssuerAsync(unitOfWork, issuer, subscriptions, now, cancellationToken);

            await unitOfWork.Commit(cancellationToken);

            return await Result<int>.SuccessAsync(issuer.Id, localizer["MessageSave"]);
        }
    }
}
