using DS.Platform.Domain.Contracts;

namespace DS.Platform.Infrastructure.Persistence.Repositories.Common
{
    /// <summary>Filters every admin list shares (BaseFiltersRequest.StateFilter: 0 = inactive, 1 = active, null = all).</summary>
    internal static class ListFilters
    {
        public static IQueryable<T> ByState<T, TId>(this IQueryable<T> query, int? stateFilter) where T : AuditableEntity<TId>
            => stateFilter switch
            {
                0 => query.Where(x => !x.IsActive),
                1 => query.Where(x => x.IsActive),
                _ => query
            };
    }
}
