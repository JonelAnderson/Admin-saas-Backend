using DS.Platform.Application.DTOs.Payments.Response;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Shared.Localization;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Localization;

namespace DS.Platform.Application.Features.Payments.Queries.GetById
{
    public class GetPaymentByIdQuery : IRequest<Result<PaymentResponseDto>>
    {
        public int Id { get; set; }
    }

    internal class GetPaymentByIdQueryHandler(
        IUnitOfWork<int> unitOfWork,
        IStringLocalizer<ServerCommonResources> localizer) : IRequestHandler<GetPaymentByIdQuery, Result<PaymentResponseDto>>
    {
        public async Task<Result<PaymentResponseDto>> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
        {
            var payment = await unitOfWork.Payments.GetResponseByIdAsync(request.Id, cancellationToken);
            if (payment is null)
            {
                return await Result<PaymentResponseDto>.FailAsync(localizer["MessageQueryEmpty"]);
            }

            return await Result<PaymentResponseDto>.SuccessAsync(payment);
        }
    }
}
