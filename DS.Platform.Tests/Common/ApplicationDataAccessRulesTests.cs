using System.Text.RegularExpressions;

namespace DS.Platform.Tests.Common
{
    /// <summary>
    /// CLAUDE.md rule (section 2): Application never reads the raw DbSet (<c>.Entities</c>, which does not filter soft-deleted rows)
    /// nor builds <c>Include</c> graphs: reads that must include deleted rows (names on historical documents) and queries with
    /// includes live in a repository method that says so. Reflection cannot see calls, so this scans the source files.
    /// </summary>
    public class ApplicationDataAccessRulesTests
    {
        // ".Entities" as a member access, but not the "Domain.Entities" namespace.
        private static readonly Regex RawDbSet = new(@"(?<!Domain)\.Entities\b", RegexOptions.Compiled);
        private static readonly Regex Include = new(@"\.(Then)?Include\(", RegexOptions.Compiled);
        // Paging by hand: Skip, or a page size clamped with Math.Min (module helpers such as "at most 200").
        private static readonly Regex HandPaging = new(@"\.Skip\(|Math\.Min\([^;]*NumRecordsPage", RegexOptions.Compiled);

        private static string ApplicationFolder()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "DS.Platform.Application");
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "DS.Platform.Application.csproj")))
                    return candidate;
            }
            throw new DirectoryNotFoundException("DS.Platform.Application source folder not found above " + AppContext.BaseDirectory);
        }

        private static List<string> Offenders(Regex pattern, params string[] exceptFiles)
        {
            var root = ApplicationFolder();
            var sep = Path.DirectorySeparatorChar;
            return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{sep}bin{sep}") && !f.Contains($"{sep}obj{sep}"))
                .Where(f => !exceptFiles.Contains(Path.GetFileName(f)))
                .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
                .Where(x => !x.Text.TrimStart().StartsWith("//") && pattern.IsMatch(x.Text))
                .Select(x => $"{Path.GetRelativePath(root, x.File)}:{x.Line}")
                .ToList();
        }

        [Fact]
        public void Application_scan_finds_the_source_files()
            => Assert.True(Directory.EnumerateFiles(ApplicationFolder(), "*.cs", SearchOption.AllDirectories).Count() > 100);

        [Fact]
        public void Application_does_not_read_the_raw_DbSet()
        {
            var offenders = Offenders(RawDbSet);
            Assert.True(offenders.Count == 0,
                "Use GetAllQueryable() or a repository method (…IncludingDeletedAsync when deleted rows must count) instead of .Entities: " + string.Join(", ", offenders));
        }

        [Fact]
        public void Application_does_not_page_by_hand()
        {
            // Lists page in the repository with ApplyPaging (Id order) or PageOf (own order), 50 rows at most.
            var offenders = Offenders(HandPaging, "QueryableExtensions.cs");
            Assert.True(offenders.Count == 0,
                "Pass the BaseFiltersRequest to the repository and page there with query.ApplyPaging(filters) / ordered.PageOf(filters): " + string.Join(", ", offenders));
        }

        [Fact]
        public void Application_does_not_build_includes()
        {
            var offenders = Offenders(Include);
            Assert.True(offenders.Count == 0, "Move the query with Include to a repository method: " + string.Join(", ", offenders));
        }
    }
}
