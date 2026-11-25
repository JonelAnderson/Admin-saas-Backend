using DS.Platform.Domain.Entities.Audit;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Domain.Enums.Identity;
using DS.Platform.Infrastructure.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DS.Platform.Infrastructure.Contexts
{
    public abstract class AuditableContext : IdentityDbContext<AdmUser, AdmRole, string, IdentityUserClaim<string>, IdentityUserRole<string>, IdentityUserLogin<string>, AdmRoleClaim, IdentityUserToken<string>>
    {
        protected AuditableContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Audit> AuditTrails { get; set; }

        /// <summary>Valor que reemplaza en la auditoría a los secretos: se registra que cambiaron, no su valor.</summary>
        public const string MaskedValue = "***";

        private static readonly HashSet<string> SensitiveUserColumns = new()
        {
            nameof(AdmUser.PasswordHash),
            nameof(AdmUser.SecurityStamp),
            nameof(AdmUser.ConcurrencyStamp),
            nameof(AdmUser.RefreshToken)
        };

        /// <summary>Hash de contraseña, stamps y refresh token del usuario; valor de los tokens de Identity (clave del autenticador, códigos de recuperación); cuerpo de la invitación al portal; API key y secret cifrados de una entrega de credenciales al ERP.</summary>
        public static bool IsSensitiveColumn(object entity, string propertyName) => entity switch
        {
            AdmUser => SensitiveUserColumns.Contains(propertyName),
            IdentityUserToken<string> => propertyName == nameof(IdentityUserToken<string>.Value),
            DS.Platform.Domain.Entities.Identity.TrustedDevice => propertyName == nameof(DS.Platform.Domain.Entities.Identity.TrustedDevice.TokenHash),
            // La API key y el secret (aun cifrados) de una entrega de credenciales al ERP nunca quedan en la auditoría.
            DS.Platform.Domain.Entities.Ops.ApiCredentialDelivery => propertyName is nameof(DS.Platform.Domain.Entities.Ops.ApiCredentialDelivery.ApiKeyProtected)
                                                                      or nameof(DS.Platform.Domain.Entities.Ops.ApiCredentialDelivery.ApiSecretProtected),
            // El cuerpo de la invitación al portal lleva el enlace con el token de activación: nunca queda en la auditoría.
            DS.Platform.Domain.Entities.Ops.Notification n => propertyName == nameof(DS.Platform.Domain.Entities.Ops.Notification.Body)
                                                              && n.Type == DS.Platform.Domain.Enums.NotificationType.PortalInvitation,
            _ => false
        };

        private static object? AuditValue(object entity, string propertyName, object? value)
            => value is not null && IsSensitiveColumn(entity, propertyName) ? MaskedValue : value;

        public virtual async Task<int> SaveChangesAsync(string userId = null!, CancellationToken cancellationToken = new())
        {
            var auditEntries = OnBeforeSaveChanges(userId);
            var result = await base.SaveChangesAsync(cancellationToken);
            await OnAfterSaveChanges(auditEntries, cancellationToken);
            return result;
        }

        private List<AuditEntry> OnBeforeSaveChanges(string userId)
        {
            ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditEntry>();
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is Audit
                    || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditEntry = new AuditEntry(entry)
                {
                    TableName = entry.Entity.GetType().Name,
                    UserId = userId,
                    IpAddress= IPHelper.GetIpAddress(),
                };
                auditEntries.Add(auditEntry);
                foreach (var property in entry.Properties)
                {
                    if (property.IsTemporary)
                    {
                        auditEntry.TemporaryProperties.Add(property);
                        continue;
                    }

                    string propertyName = property.Metadata.Name;
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue!;
                        continue;
                    }

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.AuditType = AuditType.Create;
                            auditEntry.NewValues[propertyName] = AuditValue(entry.Entity, propertyName, property.CurrentValue)!;
                            break;

                        case EntityState.Deleted:
                            auditEntry.AuditType = AuditType.Delete;
                            auditEntry.OldValues[propertyName] = AuditValue(entry.Entity, propertyName, property.OriginalValue)!;
                            break;

                        case EntityState.Modified:
                            if (property.IsModified && property.OriginalValue?.Equals(property.CurrentValue) == false)
                            {
                                auditEntry.ChangedColumns.Add(propertyName);
                                auditEntry.AuditType = AuditType.Update;
                                auditEntry.OldValues[propertyName] = AuditValue(entry.Entity, propertyName, property.OriginalValue)!;
                                auditEntry.NewValues[propertyName] = AuditValue(entry.Entity, propertyName, property.CurrentValue)!;
                            }
                            break;
                    }
                }
            }
            foreach (var auditEntry in auditEntries.Where(_ => !_.HasTemporaryProperties))
            {
                AuditTrails.Add(auditEntry.ToAudit());
            }
            return auditEntries.Where(_ => _.HasTemporaryProperties).ToList();
        }

        private Task OnAfterSaveChanges(List<AuditEntry> auditEntries, CancellationToken cancellationToken = new())
        {
            if (auditEntries == null || auditEntries.Count == 0)
                return Task.CompletedTask;

            foreach (var auditEntry in auditEntries)
            {
                foreach (var prop in auditEntry.TemporaryProperties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue!;
                    }
                    else
                    {
                        auditEntry.NewValues[prop.Metadata.Name] = AuditValue(auditEntry.Entry.Entity, prop.Metadata.Name, prop.CurrentValue)!;
                    }
                }
                AuditTrails.Add(auditEntry.ToAudit());
            }
            return SaveChangesAsync(cancellationToken);
        }
    }
}