using DS.Platform.Application.Extensions;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;

namespace DS.Platform.Tests.Common
{
    /// <summary>Regression: repositories applied Skip before OrderBy, so pages over an unordered source repeated or skipped rows.</summary>
    public class PagingTests
    {
        // Ids 1..25 stored out of order, as a database may return them.
        private static IQueryable<Plan> Source()
            => new[] { 7, 25, 3, 18, 1, 12, 22, 9, 15, 4, 20, 11, 2, 24, 6, 17, 13, 8, 23, 5, 19, 10, 21, 14, 16 }
                .Select(id => new Plan { Id = id, Product = PlanProduct.Erp, Code = $"P{id}", Name = $"S{id}" })
                .AsQueryable();

        private static int[] Ids(IQueryable<Plan> q) => q.Select(x => x.Id).ToArray();

        [Fact]
        public void Offset_pages_are_ordered_newest_first_and_do_not_overlap()
        {
            var page1 = Ids(Source().ApplyPaging(new BaseFiltersRequest { NumPage = 1, NumRecordsPage = 10 }));
            var page2 = Ids(Source().ApplyPaging(new BaseFiltersRequest { NumPage = 2, NumRecordsPage = 10 }));
            var page3 = Ids(Source().ApplyPaging(new BaseFiltersRequest { NumPage = 3, NumRecordsPage = 10 }));

            Assert.Equal(Enumerable.Range(16, 10).Reverse(), page1);
            Assert.Equal(Enumerable.Range(6, 10).Reverse(), page2);
            Assert.Equal(Enumerable.Range(1, 5).Reverse(), page3);
        }

        [Fact]
        public void Keyset_page_starts_after_the_last_id_and_download_returns_everything()
        {
            var next = Ids(Source().ApplyPaging(new BaseFiltersRequest { NumPage = 2, NumRecordsPage = 10, LastId = 16 }));
            Assert.Equal(Enumerable.Range(6, 10).Reverse(), next);

            var all = Ids(Source().ApplyPaging(new BaseFiltersRequest { NumPage = 1, NumRecordsPage = 10, Download = true }));
            Assert.Equal(Enumerable.Range(1, 25).Reverse(), all);

            // Page size 0 or negative is the default page (10), never every row; page 0 is page 1.
            var unsized = Ids(Source().ApplyPaging(new BaseFiltersRequest { NumPage = 0, NumRecordsPage = 0 }));
            Assert.Equal(Enumerable.Range(16, 10).Reverse(), unsized);
        }

        [Fact]
        public void Page_size_is_capped_at_50_whichever_name_the_client_sends()
        {
            Assert.Equal(50, new BaseFiltersRequest { NumRecordsPage = 100000 }.NumRecordsPage);
            Assert.Equal(50, new BaseFiltersRequest { Records = 500 }.NumRecordsPage);
            Assert.Equal(10, new BaseFiltersRequest { NumRecordsPage = 0 }.NumRecordsPage);
            Assert.Equal(10, new BaseFiltersRequest { Records = -5 }.NumRecordsPage);
            Assert.Equal(1, new BaseFiltersRequest { NumPage = -3 }.NumPage);
            Assert.Equal(20, new BaseFiltersRequest { Records = 20 }.NumRecordsPage);
        }

        [Fact]
        public void PageOf_keeps_the_list_own_order_and_its_pages_do_not_overlap()
        {
            // Own order (e.g. active first, then newest): Id % 3 descending, then Id descending.
            IOrderedQueryable<Plan> Ordered() => Source().OrderByDescending(x => x.Id % 3).ThenByDescending(x => x.Id);
            var expected = Ids(Ordered());

            var pages = Enumerable.Range(1, 3)
                .Select(n => Ids(Ordered().PageOf(new BaseFiltersRequest { NumPage = n, NumRecordsPage = 10 })))
                .ToList();
            Assert.Equal([10, 10, 5], pages.Select(p => p.Length));
            Assert.Equal(expected, pages.SelectMany(p => p));
            Assert.Equal([23, 20, 17, 14, 11, 8, 5, 2, 25, 22], pages[0]);

            // No cursor with an own order: LastId is ignored, the page is the offset one.
            Assert.Equal(pages[1], Ids(Ordered().PageOf(new BaseFiltersRequest { NumPage = 2, NumRecordsPage = 10, LastId = 3 })));
            // Only Download returns everything, still in the own order; page size 0 is the default page.
            Assert.Equal(expected, Ids(Ordered().PageOf(new BaseFiltersRequest { NumPage = 1, NumRecordsPage = 10, Download = true })));
            Assert.Equal(pages[0], Ids(Ordered().PageOf(new BaseFiltersRequest { NumPage = 1, NumRecordsPage = 0 })));
        }
    }
}
