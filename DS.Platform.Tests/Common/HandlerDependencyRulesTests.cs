using DS.Platform.Application.Interfaces.Repositories;
using MediatR;

namespace DS.Platform.Tests.Common
{
    /// <summary>
    /// CLAUDE.md rule: handlers reach data only through IUnitOfWork. Regression: four product handlers injected
    /// IProductRepositoryAsync directly; once it was no longer registered on its own the API did not start.
    /// </summary>
    public class HandlerDependencyRulesTests
    {
        private static readonly Type[] HandlerInterfaces = [typeof(IRequestHandler<,>), typeof(IRequestHandler<>)];

        private static IEnumerable<Type> Handlers()
            => typeof(IUnitOfWork<>).Assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false }
                            && t.GetInterfaces().Any(i => i.IsGenericType && HandlerInterfaces.Contains(i.GetGenericTypeDefinition())));

        [Fact]
        public void Handlers_get_repositories_only_through_the_unit_of_work()
        {
            var offenders = Handlers()
                .SelectMany(h => h.GetConstructors().SelectMany(c => c.GetParameters())
                    .Where(p => p.ParameterType.Namespace?.StartsWith("DS.Platform.Application.Interfaces.Repositories") == true
                                && !(p.ParameterType.IsGenericType && p.ParameterType.GetGenericTypeDefinition() == typeof(IUnitOfWork<>)))
                    .Select(p => $"{h.Name}({p.ParameterType.Name})"))
                .ToList();
            Assert.True(offenders.Count == 0, "Inject IUnitOfWork<TId> instead: " + string.Join(", ", offenders));
        }

        [Fact]
        public void Handlers_do_not_depend_on_infrastructure()
        {
            var offenders = Handlers()
                .SelectMany(h => h.GetConstructors().SelectMany(c => c.GetParameters())
                    .Where(p => p.ParameterType.Assembly.GetName().Name?.StartsWith("DS.Platform.Infrastructure") == true)
                    .Select(p => $"{h.Name}({p.ParameterType.Name})"))
                .ToList();
            Assert.True(offenders.Count == 0, "Handlers must depend on Application interfaces only: " + string.Join(", ", offenders));
        }
    }
}
