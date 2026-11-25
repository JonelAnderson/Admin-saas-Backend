using DS.Platform.Application.Interfaces.Repositories.ApiSunat;
using DS.Platform.Application.Interfaces.Repositories.Common;
using DS.Platform.Application.Interfaces.Repositories.Erp;
using DS.Platform.Application.Interfaces.Repositories.Customers;
using DS.Platform.Application.Interfaces.Repositories.Ops;
using DS.Platform.Application.Interfaces.Repositories.Payments;
using DS.Platform.Application.Interfaces.Repositories.Plans;
using DS.Platform.Application.Interfaces.Repositories.Subscriptions;
using DS.Platform.Domain.Contracts;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;

namespace DS.Platform.Application.Interfaces.Repositories
{
    public interface IUnitOfWork<TId> : IDisposable
    {
        IGenericRepositoryAsync<T, TId> Repository<T>() where T : AuditableEntity<TId>;

        IMenuRepository Menus { get; }

        // Audit trail (not an auditable entity)
        IAuditTrailRepository AuditTrails { get; }

        // Planes comerciales (módulo de referencia). Cada módulo nuevo expone aquí su repositorio específico, como este.
        IPlanRepositoryAsync Plans { get; }

        // Clientes de la plataforma y sus suscripciones (fase C2).
        ICustomerRepositoryAsync Customers { get; }
        ISubscriptionRepositoryAsync Subscriptions { get; }
        IPaymentRepositoryAsync Payments { get; }

        // Corte efectivo (fase C3): empresas emisoras de la API SUNAT y bandeja de acciones remotas.
        IApiSunatIssuerRepositoryAsync ApiSunatIssuers { get; }
        IRemoteActionRepositoryAsync RemoteActions { get; }
        INotificationRepositoryAsync Notifications { get; }

        // Aprovisionamiento en Coolify (fase C5).
        IInstanceDeploymentRepositoryAsync InstanceDeployments { get; }

        // Acceso de personas al portal de clientes de la API SUNAT (fase C6).
        IPortalAccessRepositoryAsync PortalAccesses { get; }

        // Entrega de las credenciales de la API SUNAT al ERP (fase C6b).
        IApiCredentialDeliveryRepositoryAsync ApiCredentialDeliveries { get; }

        // Instancias del ERP y sus licencias (fase C4).
        IErpInstanceRepositoryAsync ErpInstances { get; }
        ILicenseIssueRepositoryAsync LicenseIssues { get; }

        Task<int> Commit(CancellationToken cancellationToken);

        Task<int> CommitAndRemoveCache(CancellationToken cancellationToken, params string[] cacheKeys);

        IDbTransaction BeginTransaction();

        /// <summary>Several commits that must succeed or fail together (CommitAsync / RollbackAsync on the result).</summary>
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
        Task Rollback();

        /// <summary>Descarta todo lo pendiente de guardar (p. ej. tras un DbUpdateException, para que el siguiente Commit no lo reintente).</summary>
        void ClearChanges();
    }
}
