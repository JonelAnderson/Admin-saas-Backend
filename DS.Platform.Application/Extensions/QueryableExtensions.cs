using DS.Platform.Application.Exceptions;
using DS.Platform.Domain.Contracts;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Shared.Wrapper;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DS.Platform.Application.Extensions
{
    public static class QueryableExtensions
    {
        public static async Task<PaginatedResult<T>> ToPaginatedListAsync<T>(this IQueryable<T> source, int pageNumber, int pageSize) where T : class
        {
            if (source == null) throw new ApiException();
            pageNumber = pageNumber == 0 ? 1 : pageNumber;
            pageSize = pageSize == 0 ? 10 : pageSize;
            int count = await source.CountAsync();
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            List<T> items = await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
            return PaginatedResult<T>.Success(items, count, pageNumber, pageSize);
        }

        /// <summary>
        /// Page of a list ordered by Id descending (newest first). Order is applied BEFORE Skip/Take so pages are stable.
        /// With <see cref="BaseFiltersRequest.LastId"/> it is keyset pagination (Id &lt; LastId, no Skip); otherwise offset (NumPage / NumRecordsPage).
        /// Download not false (true or null: exports) returns everything (still ordered). The page size is always 1..50 (BasePaginationRequest). Count the total before calling this.
        /// </summary>
        public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> query, BaseFiltersRequest filters) where T : class, IEntity<int>
        {
            var ordered = query.OrderByDescending(x => x.Id);
            if (filters.Download != false)
                return ordered;
            if (filters.LastId is int lastId && lastId > 0)
                return ordered.Where(x => x.Id < lastId).Take(filters.NumRecordsPage);
            return ordered.Skip((filters.NumPage - 1) * filters.NumRecordsPage).Take(filters.NumRecordsPage);
        }
        /// <summary>
        /// Offset page of a list with its own order (e.g. by code or priority). The order must already be applied (IOrderedQueryable),
        /// so Skip / Take never run on an unordered set. No LastId cursor here: it only works with an Id order (use ApplyPaging).
        /// </summary>
        public static IQueryable<T> PageOf<T>(this IOrderedQueryable<T> ordered, BaseFiltersRequest filters)
        {
            if (filters.Download != false)
                return ordered;
            return ordered.Skip((filters.NumPage - 1) * filters.NumRecordsPage).Take(filters.NumRecordsPage);
        }
    }

    /// <summary><see cref="QueryableExtensions.ApplyPaging{T}"/> for entities with a <c>long</c> Id (C# picks the overload whose constraint fits).</summary>
    public static class QueryableLongIdExtensions
    {
        /// <inheritdoc cref="QueryableExtensions.ApplyPaging{T}"/>
        public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> query, BaseFiltersRequest filters) where T : class, IEntity<long>
        {
            var ordered = query.OrderByDescending(x => x.Id);
            if (filters.Download != false)
                return ordered;
            if (filters.LastId is int lastId && lastId > 0)
                return ordered.Where(x => x.Id < lastId).Take(filters.NumRecordsPage);
            return ordered.Skip((filters.NumPage - 1) * filters.NumRecordsPage).Take(filters.NumRecordsPage);
        }
    }
}