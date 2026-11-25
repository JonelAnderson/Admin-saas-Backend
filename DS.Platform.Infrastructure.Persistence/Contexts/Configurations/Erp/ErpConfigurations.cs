using DS.Platform.Domain.Entities.Erp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DS.Platform.Infrastructure.Persistence.Contexts.Configurations.Erp
{
    public class ErpInstanceConfiguration : IEntityTypeConfiguration<ErpInstance>
    {
        public void Configure(EntityTypeBuilder<ErpInstance> builder)
        {
            builder.ToTable("Instances", "Erp");
            builder.Property(x => x.Name).HasMaxLength(120);
            builder.Property(x => x.Url).HasMaxLength(300);
            builder.Property(x => x.Server).HasMaxLength(120);
            builder.Property(x => x.Notes).HasMaxLength(1000);
            builder.Property(x => x.CoolifyServiceUuid).HasMaxLength(60);
            builder.Property(x => x.DeployedVersion).HasMaxLength(40);
            builder.Property(x => x.LicenseKeyHash).HasMaxLength(64);
            builder.Property(x => x.LicenseKeyHint).HasMaxLength(8);
            builder.Property(x => x.Version).HasMaxLength(40);
            builder.Property(x => x.LastMigration).HasMaxLength(150);

            builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);

            // La clave identifica a la instancia: dos instancias nunca comparten hash (una instancia borrada libera el suyo).
            builder.HasIndex(x => x.LicenseKeyHash).IsUnique().HasFilter("\"IsDeleted\" = false");
            builder.HasIndex(x => new { x.CustomerId, x.Name }).IsUnique().HasFilter("\"IsDeleted\" = false");
        }
    }

    public class LicenseIssueConfiguration : IEntityTypeConfiguration<LicenseIssue>
    {
        public void Configure(EntityTypeBuilder<LicenseIssue> builder)
        {
            builder.ToTable("LicenseIssues", "Erp");
            builder.Property(x => x.Status).HasConversion<int>();
            builder.Property(x => x.PlanCode).HasMaxLength(40);

            builder.HasOne(x => x.Instance).WithMany().HasForeignKey(x => x.InstanceId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.InstanceId, x.IssuedOnUtc });
        }
    }
}
