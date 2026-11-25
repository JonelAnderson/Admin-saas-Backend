using DS.Platform.Application.DTOs.Payments.Response;
using DS.Platform.Application.Extensions;
using DS.Platform.Application.Interfaces.Repositories.Payments;
using DS.Platform.Application.Mappings.Payments;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Persistence.Repositories.Common;
using DS.Platform.Infrastructure.Persistence.Repository;
using DS.Platform.Shared.Commons.Bases.Request;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Infrastructure.Persistence.Repositories.Payments
{
    public class PaymentRepositoryAsync : GenericRepositoryAsync<Payment, int>, IPaymentRepositoryAsync
    {
        private readonly DbSet<Payment> _payments;

        public PaymentRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _payments = context.Set<Payment>();
        }

        public async Task<(IEnumerable<PaymentResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, PaymentFilter filter, CancellationToken cancellationToken)
        {
            var query = _payments.AsNoTracking().Where(x => !x.IsDeleted);

            if (filter.CustomerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == filter.CustomerId.Value);
            }
            if (filter.SubscriptionId.HasValue)
            {
                query = query.Where(x => x.SubscriptionId == filter.SubscriptionId.Value);
            }
            if (filter.Status.HasValue)
            {
                var status = filter.Status.Value;
                query = query.Where(x => x.Status == status);
            }
            if (filter.Method.HasValue)
            {
                var method = filter.Method.Value;
                query = query.Where(x => x.Method == method);
            }
            if (filter.From.HasValue)
            {
                var from = filter.From.Value;
                query = query.Where(x => x.PaidOn >= from);
            }
            if (filter.To.HasValue)
            {
                var to = filter.To.Value;
                query = query.Where(x => x.PaidOn <= to);
            }
            if (!string.IsNullOrEmpty(filters.TextFilter))
            {
                query = query.WhereSearch(x => x.Customer!.Ruc.Contains(filters.TextFilter)
                                               || x.Customer!.BusinessName.Contains(filters.TextFilter)
                                               || x.OperationNumber!.Contains(filters.TextFilter));
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query.OrderByDescending(x => x.PaidOn).ThenByDescending(x => x.Id)
                .PageOf(filters)
                .Select(PaymentMappings.ToResponse)
                .ToListAsync(cancellationToken);
            return (items, total);
        }

        public Task<PaymentResponseDto?> GetResponseByIdAsync(int id, CancellationToken cancellationToken)
            => _payments.AsNoTracking().Where(x => !x.IsDeleted && x.Id == id)
                .Select(PaymentMappings.ToResponse)
                .FirstOrDefaultAsync(cancellationToken);

        public Task<bool> IsOperationRegisteredAsync(PaymentMethod method, string operationNumber, CancellationToken cancellationToken)
            => _payments.AnyAsync(x => !x.IsDeleted && x.Status == PaymentStatus.Registered && x.Method == method
                                       && x.OperationNumber == operationNumber, cancellationToken);

        public Task<int?> LastRegisteredIdAsync(int subscriptionId, CancellationToken cancellationToken)
            => _payments.Where(x => !x.IsDeleted && x.SubscriptionId == subscriptionId && x.Status == PaymentStatus.Registered)
                .OrderByDescending(x => x.Id)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
