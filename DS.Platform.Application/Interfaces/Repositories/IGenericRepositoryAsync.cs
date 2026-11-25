using DS.Platform.Domain.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DS.Platform.Application.Interfaces.Repositories
{
    public interface IGenericRepositoryAsync<T, in TId> where T : class, IEntity<TId>
    {
        Task<IEnumerable<T>> GetPagedReponseAsync(int pageNumber, int pageSize);
      
        IQueryable<T> GetAllQueryable();
        IQueryable<T> Entities { get; }
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> GetSelectAsync();
        Task<IEnumerable<T>> GetSelectByIdAsync(TId id);
        Task<T?> GetByIdAsync(TId id);
        Task<T> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
        Task DeletePermanentAsync(T entity);
        Task<bool> ChangeStateAsync(int id);
        Task ChangeStateRegisterAsync(T entity);

    }
}