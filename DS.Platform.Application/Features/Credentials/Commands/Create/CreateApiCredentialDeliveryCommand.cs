using DS.Platform.Application.DTOs.Credentials.Request;
using DS.Platform.Application.Features.Credentials.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;

namespace DS.Platform.Application.Features.Credentials.Commands.Create
{
    /// <summary>
    /// Pide entregar al ERP las credenciales de la API SUNAT de un emisor (fase C6b). Solo guarda la entrega Pendiente: el despachador
    /// regenera las credenciales en la API (invalida las actuales) y el ERP las recoge con su clave de licencia (nunca dentro del caso de uso).
    /// El emisor y la instancia deben ser del mismo cliente y estar activos, como mucho hay una entrega abierta por emisor y el operador
    /// confirma de forma explícita que las credenciales actuales dejan de funcionar.
    /// </summary>
    public class CreateApiCredentialDeliveryCommand : IRequest<Result<int>>
    {
        public CreateApiCredentialDeliveryRequestDto? CreateApiCredentialDeliveryRequestDto { get; set; }
    }

    internal class CreateApiCredentialDeliveryCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime) : IRequestHandler<CreateApiCredentialDeliveryCommand, Result<int>>
    {
        public const string InstanceNotFoundMessage = "La instancia del ERP no existe.";
        public const string InstanceInactiveMessage = "La instancia del ERP está inactiva: actívela antes de entregarle credenciales.";
        public const string IssuerNotFoundMessage = "El emisor no existe.";
        public const string IssuerInactiveMessage = "El emisor está inactivo en la consola.";
        public const string CustomerInactiveMessage = "El cliente está inactivo: actívelo antes de entregar credenciales a su ERP.";
        public const string DifferentCustomerMessage = "El emisor y la instancia no son del mismo cliente.";
        public const string InvalidRucMessage = "El RUC del emisor no tiene 11 dígitos: sincronice el emisor antes de entregar sus credenciales.";
        public const string OpenMessage = "Ese emisor ya tiene una entrega de credenciales en curso: espere a que termine o cancélela.";
        public const string DoneMessage = "Entrega registrada: las credenciales se generarán en unos minutos y el ERP las recogerá solo.";

        public async Task<Result<int>> Handle(CreateApiCredentialDeliveryCommand command, CancellationToken cancellationToken)
        {
            var dto = command.CreateApiCredentialDeliveryRequestDto!;
            if (!dto.ConfirmInvalidatePrevious)
            {
                return await Result<int>.FailAsync(CredentialDeliveryRules.ConfirmMessage);
            }

            var instance = await unitOfWork.ErpInstances.GetByIdAsync(dto.InstanceId);
            if (instance is null || instance.IsDeleted)
            {
                return await Result<int>.FailAsync(InstanceNotFoundMessage);
            }
            var issuer = await unitOfWork.ApiSunatIssuers.GetByIdAsync(dto.IssuerId);
            if (issuer is null || issuer.IsDeleted)
            {
                return await Result<int>.FailAsync(IssuerNotFoundMessage);
            }
            if (instance.CustomerId != issuer.CustomerId)
            {
                return await Result<int>.FailAsync(DifferentCustomerMessage);
            }
            var customer = await unitOfWork.Customers.GetByIdAsync(instance.CustomerId);
            if (customer is null || customer.IsDeleted || !customer.IsActive)
            {
                return await Result<int>.FailAsync(CustomerInactiveMessage);
            }
            if (!instance.IsActive)
            {
                return await Result<int>.FailAsync(InstanceInactiveMessage);
            }
            if (!issuer.IsActive)
            {
                return await Result<int>.FailAsync(IssuerInactiveMessage);
            }
            if (!CredentialDeliveryRules.IsValidRuc(issuer.Ruc))
            {
                return await Result<int>.FailAsync(InvalidRucMessage);
            }
            if (await unitOfWork.ApiCredentialDeliveries.HasOpenForIssuerAsync(issuer.Id, cancellationToken))
            {
                return await Result<int>.FailAsync(OpenMessage);
            }

            var delivery = new ApiCredentialDelivery
            {
                InstanceId = instance.Id,
                CustomerId = instance.CustomerId,
                IssuerId = issuer.Id,
                TenantId = issuer.TenantId,
                Ruc = issuer.Ruc,
                Status = ApiCredentialDeliveryStatus.Pending,
                NextAttemptOnUtc = dateTime.NowUtc
            };
            await unitOfWork.ApiCredentialDeliveries.AddAsync(delivery);
            await unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(delivery.Id, DoneMessage);
        }
    }
}
