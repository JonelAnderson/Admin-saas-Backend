using DS.Platform.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DS.Platform.Tests.Support
{
    /// <summary>
    /// One in-memory database shared by several DbContext instances, each one acting as a different
    /// request (user), like the scoped DbContext in the WebApi.
    /// </summary>
    public sealed class TestDatabase
    {
        private readonly string _name = $"erp-tests-{Guid.NewGuid():N}";

        public ApplicationDbContext CreateContext(TestCurrentUserService currentUser)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(_name)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options, new FixedDateTimeService(), currentUser);
        }

        /// <summary>Seeds without a user (like startup seeding or a background job).</summary>
        public async Task SeedAsync(Action<ApplicationDbContext> seed)
        {
            await using var db = CreateContext(TestCurrentUserService.NoContext());
            seed(db);
            await db.SaveChangesAsync();
        }
    }
}
