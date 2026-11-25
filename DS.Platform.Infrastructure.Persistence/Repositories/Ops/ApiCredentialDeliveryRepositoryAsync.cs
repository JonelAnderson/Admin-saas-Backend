using DS.Platform.Application.DTOs.Credentials.Response;
using DS.Platform.Application.Extensions;
using DS.Platform.Application.Interfaces.Repositories.Ops;
using DS.Platform.Application.Mappings.Credentials;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Domain.Enums;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Persistence.Repositories.Common;
using DS.Platform.Infrastructure.Persistence.Repository;
using DS.Platform.Shared.Commons.Bases.Request;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Infrastructure.Persistence.Repositories.Ops
{
    public class ApiCredentialDeliveryRepositoryAsync : GenericRepositoryAsync<ApiCredentialDelivery, int>, IApiCredentialDeliveryRepositoryAsync
    {
        private readonly DbSet<ApiCredentialDelivery> _deliveries;

        public ApiCredentialDeliveryRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _deliveries = context.Set<ApiCredentialDelivery>();
        }

        public async Task<(IEnumerable<ApiCredentialDeliveryResponseDto>, int)> GetAllPaginatedAsync(
            BaseFiltersRequest filters, ApiCredentialDeliveryFilter filter, CancellationToken cancellationToken)
        {
            var query = _deliveries.AsNoTracking().Where(x => !x.IsDeleted);

            if (filter.Status.HasValue)
            {
                var status = filter.Status.Value;
                query = query.Where(x => x.Status == status);
            }
            if (filter.CustomerId.HasValue)
            {
                query = query.Where(x => x.CustomerId == filter.CustomerId.Value);
            }
            if (filter.InstanceId.HasValue)
            {
                query = query.Where(x => x.InstanceId == filter.InstanceId.Value);
            }
            if (!string.IsNullOrEmpty(filters.TextFilter))
            {
                query = query.WhereSearch(x => x.Ruc.Contains(filters.TextFilter)
                                               || x.Issuer!.BusinessName.Contains(filters.TextFilter)
                                               || x.Instance!.Name.Contains(filters.TextFilter)
                                               || x.Customer!.BusinessName.Contains(filters.TextFilter)
                                               || x.Customer!.Ruc.Contains(filters.TextFilter));
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(x => x.Id)
                .PageOf(filters)
                .Select(ApiCredentialDeliveryMappings.ToResponse)
                .ToListAsync(cancellationToken);
            return (items, total);
        }

        public Task<bool> HasOpenForIssuerAsync(int issuerId, CancellationToken cancellationToken)
        {
            // Estados abiertos: Pendiente, En curso, Lista y Fallida (variables locales: EF no traduce conversiones de enum dentro de la expresión).
            var pending = ApiCredentialDeliveryStatus.Pending;
            var sending = ApiCredentialDeliveryStatus.Sending;
            var ready = ApiCredentialDeliveryStatus.Ready;
            var failed = ApiCredentialDeliveryStatus.Failed;
            return _deliveries.AsNoTracking().AnyAsync(
                x => !x.IsDeleted && x.IssuerId == issuerId
                     && (x.Status == pending || x.Status == sending || x.Status == ready || x.Status == failed),
                cancellationToken);
        }

        public Task<List<ApiCredentialDelivery>> GetDueAsync(DateTime nowUtc, int take, CancellationToken cancellationToken)
        {
            var pending = ApiCredentialDeliveryStatus.Pending;
            var failed = ApiCredentialDeliveryStatus.Failed;
            var sending = ApiCredentialDeliveryStatus.Sending;
            return _deliveries
                .Where(x => !x.IsDeleted
                            && (((x.Status == pending || x.Status == failed) && x.NextAttemptOnUtc <= nowUtc)
                                || (x.Status == sending && x.LockedUntilUtc != null && x.LockedUntilUtc < nowUtc)))
                .OrderBy(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public Task<List<ApiCredentialDelivery>> GetExpiredReadyAsync(DateTime nowUtc, int take, CancellationToken cancellationToken)
        {
            var ready = ApiCredentialDeliveryStatus.Ready;
            return _deliveries
                .Where(x => !x.IsDeleted && x.Status == ready && x.ExpiresOnUtc != null && x.ExpiresOnUtc <= nowUtc)
                .OrderBy(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public Task<List<ApiCredentialDelivery>> GetReadyByInstanceAsync(int instanceId, DateTime nowUtc, int take, CancellationToken cancellationToken)
        {
            var ready = ApiCredentialDeliveryStatus.Ready;
            return _deliveries
                .Where(x => !x.IsDeleted && x.InstanceId == instanceId && x.Status == ready
                            && (x.ExpiresOnUtc == null || x.ExpiresOnUtc > nowUtc))
                .OrderBy(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public Task<List<ApiCredentialDelivery>> GetByIdsForInstanceAsync(int instanceId, IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
            => _deliveries
                .Where(x => !x.IsDeleted && x.InstanceId == instanceId && ids.Contains(x.Id))
                .ToListAsync(cancellationToken);
    }
}
