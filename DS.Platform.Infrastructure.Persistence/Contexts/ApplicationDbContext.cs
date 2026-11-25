using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Contracts;
using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Domain.Entities.Menus;
using DS.Platform.Infrastructure.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DS.Platform.Infrastructure.Persistence.Contexts
{
    /// <summary>
    /// Base de datos de la consola de plataforma. A diferencia del ERP no es multiempresa: no hay filtro por empresa.
    /// Cada módulo nuevo agrega su <c>DbSet</c> y su <c>IEntityTypeConfiguration</c> en <c>Contexts/Configurations/&lt;Modulo&gt;</c>
    /// (se aplican todas las del ensamblado).
    /// </summary>
    public class ApplicationDbContext : AuditableContext
    {
        private readonly IDateTimeService _dateTime;
        private readonly ICurrentUserService _currentUserService;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options,
            IDateTimeService dateTime, ICurrentUserService currentUserService) : base(options)
        {
            _dateTime = dateTime;
            _currentUserService = currentUserService;
        }

        public virtual DbSet<Menu> Menus { get; set; } = null!;
        public virtual DbSet<MenuRole> MenuRoles { get; set; } = null!;
        public virtual DbSet<TrustedDevice> TrustedDevices { get; set; } = null!;

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new())
        {
            if (_currentUserService.UserId == null)
            {
                return await base.SaveChangesAsync(cancellationToken);
            }
            return await SaveChangesAsync(_currentUserService.UserId, cancellationToken);
        }

        /// <summary>
        /// Also reached by a parameterless <c>SaveChangesAsync()</c> call (C# overload resolution picks this one);
        /// stamping must run before the audit trail captures the values.
        /// </summary>
        public override Task<int> SaveChangesAsync(string userId = null!, CancellationToken cancellationToken = new())
        {
            PrepareChangesForSave();
            return base.SaveChangesAsync(userId ?? _currentUserService.UserId!, cancellationToken);
        }

        /// <summary>Every EF Core async save ends here, so no caller can skip stamping.</summary>
        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            PrepareChangesForSave();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            PrepareChangesForSave();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        /// <summary>Idempotent: may run more than once per save (audit overload, then EF core path).</summary>
        private void PrepareChangesForSave()
        {
            foreach (var entry in ChangeTracker.Entries<IAuditableEntity>().ToList())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedOn = _dateTime.NowUtc;
                        entry.Entity.IsActive = true;
                        entry.Entity.IsDeleted = false;
                        entry.Entity.CreatedBy = _currentUserService.UserId ?? entry.Entity.CreatedBy;
                        break;

                    case EntityState.Modified:
                        entry.Entity.LastModifiedOn = _dateTime.NowUtc;
                        entry.Entity.LastModifiedBy = _currentUserService.UserId;
                        break;
                }
            }
        }

        /// <summary>Every DateTime is stored as UTC (timestamptz): see <see cref="UtcDateTimeConverter"/>.</summary>
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
            configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
            // Montos de la consola: numeric(12,2) salvo que la configuración de la entidad diga otra cosa.
            configurationBuilder.Properties<decimal>().HavePrecision(12, 2);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<AdmUser>(entity =>
            {
                entity.ToTable(name: "Users", "Identity");
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.CreatedBy).HasMaxLength(128);
                entity.Property(e => e.LastModifiedBy).HasMaxLength(128);
                entity.HasOne(u => u.User)
                  .WithMany(m => m.Subordinates)
                  .HasForeignKey(u => u.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AdmRole>(entity =>
            {
                entity.ToTable(name: "Roles", "Identity");
                entity.Property(e => e.CreatedBy).HasMaxLength(128);
                entity.Property(e => e.LastModifiedBy).HasMaxLength(128);
            });
            builder.Entity<IdentityUserRole<string>>(entity => entity.ToTable("UserRoles", "Identity"));
            builder.Entity<IdentityUserClaim<string>>(entity => entity.ToTable("UserClaims", "Identity"));
            builder.Entity<IdentityUserLogin<string>>(entity => entity.ToTable("UserLogins", "Identity"));
            builder.Entity<IdentityUserToken<string>>(entity => entity.ToTable("UserTokens", "Identity"));

            builder.Entity<AdmRoleClaim>(entity =>
            {
                entity.ToTable(name: "RoleClaims", "Identity");
                entity.Property(e => e.CreatedBy).HasMaxLength(128);
                entity.Property(e => e.LastModifiedBy).HasMaxLength(128);
                entity.HasOne(d => d.Role)
                    .WithMany(p => p.RoleClaims)
                    .HasForeignKey(d => d.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Dispositivos recordados del login (MFA): el hash del token es único; se borran con el usuario.
            builder.Entity<TrustedDevice>(entity =>
            {
                entity.ToTable("TrustedDevices", "Identity");
                entity.Property(d => d.UserId).HasMaxLength(450);
                entity.Property(d => d.TokenHash).HasMaxLength(64);
                entity.Property(d => d.Label).HasMaxLength(200);
                entity.HasIndex(d => d.TokenHash).IsUnique();
                entity.HasIndex(d => d.UserId);
                entity.HasOne<AdmUser>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // Configuraciones de los módulos de la consola (Contexts/Configurations/<Modulo>/*Configuration.cs).
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            // Columnas de auditoría de las entidades de negocio: como en el ERP, 128 caracteres.
            foreach (var property in builder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.Name is "LastModifiedBy" or "CreatedBy" && p.GetMaxLength() is null))
            {
                property.SetMaxLength(128);
            }

            // Concurrencia optimista (IConcurrencyChecked): la versión es la columna de sistema xmin de PostgreSQL; Npgsql la
            // mapea sin crear columna. En los tests (InMemory) la propiedad sombra queda en 0 y no cambia nada.
            foreach (var entityType in builder.Model.GetEntityTypes()
                .Where(t => t.BaseType is null && typeof(IConcurrencyChecked).IsAssignableFrom(t.ClrType))
                .ToList())
            {
                builder.Entity(entityType.ClrType).Property<uint>(RowVersionProperty).IsRowVersion();
            }
        }

        /// <summary>Propiedad sombra de la versión de fila (xmin) de las entidades <see cref="IConcurrencyChecked"/>.</summary>
        public const string RowVersionProperty = "xmin";
    }
}
