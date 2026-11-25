using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Domain.Contracts;
using DS.Platform.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace DS.Platform.Infrastructure.Persistence.Repository
{
    public class GenericRepositoryAsync<T, TId> : IGenericRepositoryAsync<T, TId> where T : AuditableEntity<TId>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly DbSet<T> _entity;
        public GenericRepositoryAsync(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
            _entity = _dbContext.Set<T>();
        }

        public IQueryable<T> Entities => _entity;

        public async Task<IEnumerable<T>> GetPagedReponseAsync(int pageNumber, int pageSize)
        {
            return await _entity
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();
        }

     

        public IQueryable<T> GetAllQueryable()
        {
            var getAllQuery = GetEntityQuery(x => x.IsDeleted == false);
            return getAllQuery;
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _entity.ToListAsync();
        }

        public virtual async Task<T?> GetByIdAsync(TId id)
        {
            return await _entity.FindAsync(id);
        }
        public async Task<IEnumerable<T>> GetSelectByIdAsync(TId id)
        {
            var results = await _entity
              .Where(item => item.Id!.Equals(id))
              .AsNoTracking()
              .ToListAsync();

            return results;
        }
        public async Task<IEnumerable<T>> GetSelectAsync()
        {
            var getAllAsync = await _entity
                .Where(x => x.IsActive==true && x.IsDeleted == false)
                .AsNoTracking()
                .ToListAsync();
            return getAllAsync;
        }

        public async Task<T> AddAsync(T entity)
        {
            await _dbContext.AddAsync(entity);
            return entity;
        }

        public Task UpdateAsync(T entity)
        {
            T exist = _entity.Find(entity.Id)!;

            _dbContext.Entry(exist).CurrentValues.SetValues(entity);

            return Task.CompletedTask;
        }

        public async Task DeleteAsync(T entity)
        {
            T? exist = _entity.Find(entity.Id);
            if (exist != null)
            {
                entity.DeletedOn = DateTime.UtcNow.AddHours(-5);
                entity.IsDeleted = true;
                entity.IsActive = false;
                _dbContext.Update(entity);
            }
            await Task.CompletedTask;
        }

        public Task DeletePermanentAsync(T entity)
        {
            _dbContext.Set<T>().Remove(entity);
            return Task.CompletedTask;
        }
    

        public async Task<bool> ChangeStateAsync(int id)
        {
            T? entity = await _entity.FindAsync(id);

            entity!.IsActive = !entity.IsActive;
            entity.LastModifiedOn = DateTime.UtcNow;
            _dbContext.Update(entity);
            var recordsAffected = await _dbContext.SaveChangesAsync();

            return recordsAffected > 0;
        }


        public async Task ChangeStateRegisterAsync(T entity)
        {
            T? exist = _entity.Find(entity.Id);
            if (exist != null)
            {
                _dbContext.Update(entity);
            }
            await Task.CompletedTask;
        }
   

        public async Task<T> GetLastRegisterAsync()
        {
            var lastEntity = await _entity
                .Where(x => x.IsDeleted == false)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            //if (lastEntity == null)
            //{
            //    throw new InvalidOperationException("No records found in the database.");
            //}

            return lastEntity!;
        }

        public IQueryable<T> GetEntityQuery(Expression<Func<T, bool>>? filter = null)
        {
            IQueryable<T> query = _entity;

            if (filter != null) query = query.Where(filter);

            return query;
        }

     
    }
}