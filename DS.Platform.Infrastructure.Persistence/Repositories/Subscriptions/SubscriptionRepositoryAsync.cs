using DS.Platform.Application.DTOs.Subscriptions.Response;
using DS.Platform.Application.Extensions;
using DS.Platform.Application.Interfaces.Repositories.Subscriptions;
using DS.Platform.Application.Mappings.Subscriptions;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Persistence.Repositories.Common;
using DS.Platform.Infrastructure.Persistence.Repository;
using DS.Platform.Shared.Commons.Bases.Request;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Infrastructure.Persistence.Repositories.Subscriptions
{
    public class SubscriptionRepositoryAsync : GenericRepositoryAsync<Subscription, int>, ISubscriptionRepositoryAsync
    {
        private readonly DbSet<Subscription> _subscriptions;

        public SubscriptionRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _subscriptions = context.Set<Subscription>();
        }

        public async Task<(IEnumerable<SubscriptionResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, SubscriptionFilter filter, CancellationToken cancellationToken)
        {
            var query = _subscriptions.AsNoTracking().Where(x => !x.IsDeleted);

            if (filter.CustomerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == filter.CustomerId.Value);
            }
            if (filter.PlanId.HasValue)
            {
                query = query.Where(x => x.PlanId == filter.PlanId.Value);
            }
            if (filter.Product.HasValue)
            {
                var product = filter.Product.Value;
                query = query.Where(x => x.Product == product);
            }
            if (filter.Status.HasValue)
            {
                var status = filter.Status.Value;
                query = query.Where(x => x.Status == status);
            }
            if (!string.IsNullOrEmpty(filters.TextFilter))
            {
                query = query.WhereSearch(x => x.Customer!.Ruc.Contains(filters.TextFilter)
                                               || x.Customer!.BusinessName.Contains(filters.TextFilter)
                                               || x.Plan!.Name.Contains(filters.TextFilter));
            }

            var total = await query.CountAsync(cancellationToken);
            // Las canceladas al final; el resto por fecha de suspensión (las que vencen antes, primero).
            var items = await query
                .OrderBy(x => x.Status == SubscriptionStatus.Cancelled)
                .ThenBy(x => x.SuspendOn)
                .ThenBy(x => x.Id)
                .PageOf(filters)
                .Select(SubscriptionMappings.ToResponse)
                .ToListAsync(cancellationToken);
            return (items, total);
        }

        public Task<SubscriptionResponseDto?> GetResponseByIdAsync(int id, CancellationToken cancellationToken)
            => _subscriptions.AsNoTracking().Where(x => !x.IsDeleted && x.Id == id)
                .Select(SubscriptionMappings.ToResponse)
                .FirstOrDefaultAsync(cancellationToken);

        public Task<bool> HasOpenAsync(int customerId, PlanProduct product, int? excludeId, CancellationToken cancellationToken)
            => _subscriptions.AnyAsync(x => !x.IsDeleted && x.CustomerId == customerId && x.Product == product
                                            && x.Status != SubscriptionStatus.Cancelled
                                            && (excludeId == null || x.Id != excludeId), cancellationToken);

        // Incluye las borradas a propósito: la prueba gratuita se da una sola vez por cliente y producto.
        public Task<bool> EverSubscribedIncludingDeletedAsync(int customerId, PlanProduct product, CancellationToken cancellationToken)
            => _subscriptions.AnyAsync(x => x.CustomerId == customerId && x.Product == product, cancellationToken);

        public Task<bool> CustomerHasOpenAsync(int customerId, CancellationToken cancellationToken)
            => _subscriptions.AnyAsync(x => !x.IsDeleted && x.CustomerId == customerId && x.Status != SubscriptionStatus.Cancelled,
                cancellationToken);

        public Task<bool> PlanHasOpenAsync(int planId, CancellationToken cancellationToken)
            => _subscriptions.AnyAsync(x => !x.IsDeleted && x.PlanId == planId && x.Status != SubscriptionStatus.Cancelled,
                cancellationToken);

        public Task<List<Subscription>> GetErpWithPlanByCustomerAsync(int customerId, CancellationToken cancellationToken)
            => _subscriptions.AsNoTracking().Include(x => x.Plan)
                .Where(x => !x.IsDeleted && x.CustomerId == customerId && x.Product == PlanProduct.Erp)
                .ToListAsync(cancellationToken);

        public Task<List<Subscription>> GetErpByCustomersAsync(IReadOnlyCollection<int> customerIds, CancellationToken cancellationToken)
        {
            var ids = customerIds.ToList();
            return _subscriptions.AsNoTracking()
                .Where(x => !x.IsDeleted && x.Product == PlanProduct.Erp && ids.Contains(x.CustomerId))
                .ToListAsync(cancellationToken);
        }

        public Task<List<Subscription>> GetByCustomerAsync(int customerId, CancellationToken cancellationToken)
            => _subscriptions.Include(x => x.Plan).Where(x => !x.IsDeleted && x.CustomerId == customerId).OrderBy(x => x.Id).ToListAsync(cancellationToken);

        public Task<List<int>> GetCustomerIdsByPlanAsync(int planId, CancellationToken cancellationToken)
            => _subscriptions.AsNoTracking().Where(x => !x.IsDeleted && x.PlanId == planId && x.Status != SubscriptionStatus.Cancelled)
                .Select(x => x.CustomerId).Distinct().ToListAsync(cancellationToken);

        public Task<List<Subscription>> GetOpenForEvaluationAsync(CancellationToken cancellationToken)
            => _subscriptions.Where(x => !x.IsDeleted && x.Status != SubscriptionStatus.Cancelled)
                .OrderBy(x => x.Id)
                .ToListAsync(cancellationToken);
    }
}
