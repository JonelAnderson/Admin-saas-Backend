using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Repositories.Common;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Contracts;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Infrastructure.Persistence.Repositories;
using DS.Platform.Infrastructure.Persistence.Repositories.Common;
using DS.Platform.Application.Interfaces.Repositories.ApiSunat;
using DS.Platform.Application.Interfaces.Repositories.Ops;
using DS.Platform.Application.Interfaces.Repositories.Erp;
using DS.Platform.Infrastructure.Persistence.Repositories.Erp;
using DS.Platform.Infrastructure.Persistence.Repositories.ApiSunat;
using DS.Platform.Infrastructure.Persistence.Repositories.Ops;
using DS.Platform.Application.Interfaces.Repositories.Plans;
using DS.Platform.Infrastructure.Persistence.Repositories.Plans;
using DS.Platform.Application.Interfaces.Repositories.Payments;
using DS.Platform.Infrastructure.Persistence.Repositories.Payments;
using DS.Platform.Application.Interfaces.Repositories.Customers;
using DS.Platform.Application.Interfaces.Repositories.Subscriptions;
using DS.Platform.Infrastructure.Persistence.Repositories.Customers;
using DS.Platform.Infrastructure.Persistence.Repositories.Subscriptions;
using DS.Platform.Infrastructure.Persistence.Repository;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Collections;
using System.Data;

namespace DS.Platform.Infrastructure.Repositories
{
    public class UnitOfWork<TId> : IUnitOfWork<TId>
    {
        private readonly ApplicationDbContext _dbContext;
        private bool disposed;
        private Hashtable? _repositories;
        private readonly IAppCache _cache;
        private readonly ICurrentUserService? _currentUser;

        public UnitOfWork(ApplicationDbContext dbContext, ICurrentUserService currentUserService, IAppCache cache)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _cache = cache;
            _currentUser = currentUserService;
        }

        public IGenericRepositoryAsync<TEntity, TId> Repository<TEntity>() where TEntity : AuditableEntity<TId>
        {
            _repositories ??= new Hashtable();

            var type = typeof(TEntity).Name;

            if (!_repositories.ContainsKey(type))
            {
                var repositoryType = typeof(GenericRepositoryAsync<,>);
                var repositoryInstance = Activator.CreateInstance(repositoryType.MakeGenericType(typeof(TEntity), typeof(TId)), _dbContext);
                _repositories.Add(type, repositoryInstance);
            }

            return (IGenericRepositoryAsync<TEntity, TId>)_repositories[type]!;
        }

        private IMenuRepository? _menus;
        private IAuditTrailRepository? _auditTrails;
        private IPlanRepositoryAsync? _plans;
        private ICustomerRepositoryAsync? _customers;
        private ISubscriptionRepositoryAsync? _subscriptions;
        private IPaymentRepositoryAsync? _payments;
        private IApiSunatIssuerRepositoryAsync? _apiSunatIssuers;
        private IRemoteActionRepositoryAsync? _remoteActions;
        private INotificationRepositoryAsync? _notifications;
        private IInstanceDeploymentRepositoryAsync? _instanceDeployments;
        private IPortalAccessRepositoryAsync? _portalAccesses;
        private IApiCredentialDeliveryRepositoryAsync? _apiCredentialDeliveries;
        private IErpInstanceRepositoryAsync? _erpInstances;
        private ILicenseIssueRepositoryAsync? _licenseIssues;

        public IMenuRepository Menus => _menus ??= new MenuRepository(_dbContext);
        public IAuditTrailRepository AuditTrails => _auditTrails ??= new AuditTrailRepository(_dbContext);

        public IPlanRepositoryAsync Plans => _plans ??= new PlanRepositoryAsync(_dbContext);
        public ICustomerRepositoryAsync Customers => _customers ??= new CustomerRepositoryAsync(_dbContext);
        public ISubscriptionRepositoryAsync Subscriptions => _subscriptions ??= new SubscriptionRepositoryAsync(_dbContext);
        public IPaymentRepositoryAsync Payments => _payments ??= new PaymentRepositoryAsync(_dbContext);
        public IApiSunatIssuerRepositoryAsync ApiSunatIssuers => _apiSunatIssuers ??= new ApiSunatIssuerRepositoryAsync(_dbContext);
        public IRemoteActionRepositoryAsync RemoteActions => _remoteActions ??= new RemoteActionRepositoryAsync(_dbContext);
        public INotificationRepositoryAsync Notifications => _notifications ??= new NotificationRepositoryAsync(_dbContext);
        public IInstanceDeploymentRepositoryAsync InstanceDeployments => _instanceDeployments ??= new InstanceDeploymentRepositoryAsync(_dbContext);
        public IPortalAccessRepositoryAsync PortalAccesses => _portalAccesses ??= new PortalAccessRepositoryAsync(_dbContext);
        public IApiCredentialDeliveryRepositoryAsync ApiCredentialDeliveries => _apiCredentialDeliveries ??= new ApiCredentialDeliveryRepositoryAsync(_dbContext);
        public IErpInstanceRepositoryAsync ErpInstances => _erpInstances ??= new ErpInstanceRepositoryAsync(_dbContext);
        public ILicenseIssueRepositoryAsync LicenseIssues => _licenseIssues ??= new LicenseIssueRepositoryAsync(_dbContext);

        // Repositorios específicos de los módulos de la consola: una propiedad perezosa por agregado, como Plans.

        public async Task<int> Commit(CancellationToken cancellationToken)
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> CommitAndRemoveCache(CancellationToken cancellationToken, params string[] cacheKeys)
        {
            var result = await _dbContext.SaveChangesAsync(cancellationToken);
            foreach (var cacheKey in cacheKeys)
            {
                _cache.Remove(cacheKey);
            }
            return result;
        }

        public Task Rollback()
        {
            _dbContext.ChangeTracker.Entries().ToList().ForEach(x => x.Reload());
            return Task.CompletedTask;
        }

        public void ClearChanges() => _dbContext.ChangeTracker.Clear();

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
            => _dbContext.Database.BeginTransactionAsync(cancellationToken);

        public IDbTransaction BeginTransaction()
        {
            var transaction = _dbContext.Database.BeginTransaction();
            return transaction.GetDbTransaction();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed && disposing)
            {
                _dbContext.Dispose();
            }
            disposed = true;
        }
    }
}
