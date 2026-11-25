using DS.Platform.Application.DTOs.Portal.Request;
using DS.Platform.Application.Features.Portal.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Wrapper;
using MediatR;

namespace DS.Platform.Application.Features.Portal.Commands.Invite
{
    /// <summary>
    /// Invita (o reinvita) a una persona al portal de clientes de la API SUNAT con acceso a empresas emisoras de un cliente. Solo
    /// guarda el acceso con su trabajo pendiente: el despachador llama a la API y envía el correo (nunca dentro del caso de uso).
    /// Reinvitar es invitar de nuevo al mismo correo: reemplaza las empresas, vuelve a habilitar y, si se pide, restablece el MFA.
    /// </summary>
    public class InviteToPortalCommand : IRequest<Result<int>>
    {
        public InviteToPortalRequestDto? InviteToPortalRequestDto { get; set; }
    }

    internal class InviteToPortalCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime) : IRequestHandler<InviteToPortalCommand, Result<int>>
    {
        public const string CustomerNotFoundMessage = "El cliente no existe.";
        public const string CustomerInactiveMessage = "El cliente está inactivo: actívelo antes de invitar a alguien a su portal.";
        public const string IssuerNotOfCustomerMessage = "Alguna de las empresas elegidas no está vinculada a ese cliente.";
        public const string EmailOfOtherCustomerMessage = "Ese correo ya tiene acceso al portal de otro cliente: un correo pertenece a un solo cliente.";
        public const string BusyMessage = "La invitación de ese correo se está enviando en este momento: inténtelo en un minuto.";
        public const string DoneMessage = "Invitación registrada: se enviará al correo de la persona en unos minutos.";

        public async Task<Result<int>> Handle(InviteToPortalCommand command, CancellationToken cancellationToken)
        {
            var dto = command.InviteToPortalRequestDto!;
            var email = PortalRules.NormalizeEmail(dto.Email);

            var customer = await unitOfWork.Customers.GetByIdAsync(dto.CustomerId);
            if (customer is null || customer.IsDeleted)
            {
                return await Result<int>.FailAsync(CustomerNotFoundMessage);
            }
            if (!customer.IsActive)
            {
                return await Result<int>.FailAsync(CustomerInactiveMessage);
            }

            var issuerIds = dto.IssuerIds.Distinct().ToList();
            var ownIssuers = await unitOfWork.ApiSunatIssuers.GetByCustomerAsync(customer.Id, cancellationToken);
            if (issuerIds.Count == 0 || issuerIds.Any(id => ownIssuers.All(i => i.Id != id)))
            {
                return await Result<int>.FailAsync(IssuerNotOfCustomerMessage);
            }

            var now = dateTime.NowUtc;
            var access = await unitOfWork.PortalAccesses.GetByEmailWithIssuersAsync(email, cancellationToken);
            if (access is null)
            {
                access = new PortalAccess { CustomerId = customer.Id, Email = email, Name = dto.Name.Trim() };
                access.Issuers = issuerIds.Select(id => new PortalAccessIssuer { IssuerId = id }).ToList();
                Reset(access, dto, now);
                await unitOfWork.PortalAccesses.AddAsync(access);
            }
            else
            {
                if (access.CustomerId != customer.Id)
                {
                    return await Result<int>.FailAsync(EmailOfOtherCustomerMessage);
                }
                if (access.Status == RemoteActionStatus.Sending && access.LockedUntilUtc > now)
                {
                    return await Result<int>.FailAsync(BusyMessage);
                }

                access.Name = dto.Name.Trim();
                foreach (var gone in access.Issuers.Where(i => !issuerIds.Contains(i.IssuerId)).ToList())
                {
                    access.Issuers.Remove(gone);
                }
                foreach (var id in issuerIds.Where(id => access.Issuers.All(i => i.IssuerId != id)))
                {
                    access.Issuers.Add(new PortalAccessIssuer { IssuerId = id });
                }
                Reset(access, dto, now);
                await unitOfWork.PortalAccesses.UpdateAsync(access);
            }

            await unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(access.Id, DoneMessage);
        }

        private static void Reset(PortalAccess access, InviteToPortalRequestDto dto, DateTime now)
        {
            access.IsEnabled = true;
            access.ResetMfa = dto.ResetMfa;
            access.WorkType = PortalWorkType.Invite;
            access.Status = RemoteActionStatus.Pending;
            access.Attempts = 0;
            access.NextAttemptOnUtc = now;
            access.LockedUntilUtc = null;
            access.LastError = null;
            access.DoneOnUtc = null;
        }
    }
}
