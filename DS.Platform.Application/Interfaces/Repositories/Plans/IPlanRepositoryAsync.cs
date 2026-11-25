using DS.Platform.Application.DTOs.Plans.Response;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;

namespace DS.Platform.Application.Interfaces.Repositories.Plans
{
    public interface IPlanRepositoryAsync : IGenericRepositoryAsync<Plan, int>
    {
        /// <summary>Página del listado ya proyectada al DTO, ordenada por producto, orden y nombre.</summary>
        Task<(IEnumerable<PlanResponseDto>, int)> GetAllPaginatedAsync(BaseFiltersRequest filters, PlanProduct? product, CancellationToken cancellationToken);

        /// <summary>Planes activos para combos (de un producto o de todos).</summary>
        Task<IEnumerable<PlanSelectResponseDto>> GetSelectAsync(PlanProduct? product, CancellationToken cancellationToken);

        /// <summary>true si ningún otro plan no borrado del producto usa el código (sin distinguir mayúsculas).</summary>
        Task<bool> IsUniqueCodeAsync(PlanProduct product, string code, int? excludeId, CancellationToken cancellationToken);
    }
}
