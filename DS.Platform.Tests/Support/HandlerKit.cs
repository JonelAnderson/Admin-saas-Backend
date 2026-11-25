using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Repositories;
using LazyCache;

namespace DS.Platform.Tests.Support
{
    /// <summary>Builds what CQRS handlers receive: a Unit of Work over the test context (mapping is explicit, no AutoMapper).</summary>
    internal static class HandlerKit
    {
        public static UnitOfWork<int> UnitOfWork(ApplicationDbContext db, TestCurrentUserService user) => new(db, user, new CachingService());

        public static UnitOfWork<TId> UnitOfWork<TId>(ApplicationDbContext db, TestCurrentUserService user) => new(db, user, new CachingService());
    }
}
