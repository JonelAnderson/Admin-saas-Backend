using System.Text.RegularExpressions;

namespace DS.Platform.Tests.Common
{
    /// <summary>
    /// CLAUDE.md (sección 1): el mapeo entidad ↔ DTO es explícito (<c>Mappings/&lt;Modulo&gt;/&lt;Entidad&gt;Mappings.cs</c>, y
    /// <c>Infrastructure.Persistence/Mappings/IdentityMappings.cs</c> para Identity). AutoMapper se quitó del proyecto (su
    /// licencia pasó a ser de pago): ni el paquete, ni IMapper, ni perfiles en ningún proyecto del backend.
    /// </summary>
    public class ExplicitMappingRulesTests
    {
        private static readonly Regex AutoMapperUse = new(@"\bIMapper\b|using AutoMapper;|:\s*Profile\b|\bCreateMap<|AddAutoMapper", RegexOptions.Compiled);

        private static string BackendFolder()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "DS.Platform.Application", "DS.Platform.Application.csproj")))
                    return dir.FullName;
            }
            throw new DirectoryNotFoundException("Backend source folder not found above " + AppContext.BaseDirectory);
        }

        private static IEnumerable<string> SourceFiles(string pattern)
        {
            var root = BackendFolder();
            var sep = Path.DirectorySeparatorChar;
            return Directory.GetDirectories(root, "DS.Platform.*")
                .Where(d => !Path.GetFileName(d).EndsWith(".Tests", StringComparison.Ordinal))
                .SelectMany(d => Directory.EnumerateFiles(d, pattern, SearchOption.AllDirectories))
                .Where(f => !f.Contains($"{sep}bin{sep}") && !f.Contains($"{sep}obj{sep}"));
        }

        [Fact]
        public void The_backend_does_not_use_AutoMapper()
        {
            var root = BackendFolder();
            var offenders = SourceFiles("*.cs")
                .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
                .Where(x => !x.Text.TrimStart().StartsWith("//") && AutoMapperUse.IsMatch(x.Text))
                .Select(x => $"{Path.GetRelativePath(root, x.File)}:{x.Line}")
                .ToList();

            Assert.True(offenders.Count == 0,
                "El mapeo es explícito: use (o agregue) los métodos de Mappings/<Modulo>/<Entidad>Mappings.cs, no AutoMapper: "
                + string.Join(", ", offenders));
        }

        [Fact]
        public void No_project_references_the_AutoMapper_package()
        {
            var offenders = SourceFiles("*.csproj").Where(f => File.ReadAllText(f).Contains("\"AutoMapper", StringComparison.Ordinal)).ToList();
            Assert.True(offenders.Count == 0, "Quite la referencia al paquete AutoMapper: " + string.Join(", ", offenders));
        }
    }
}
