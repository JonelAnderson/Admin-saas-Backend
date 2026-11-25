using DS.Platform.Domain.Entities.Ops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DS.Platform.Infrastructure.Persistence.Contexts.Configurations.Ops
{
    public class PortalAccessConfiguration : IEntityTypeConfiguration<PortalAccess>
    {
        public void Configure(EntityTypeBuilder<PortalAccess> builder)
        {
            builder.ToTable("PortalAccesses", "Ops");
            builder.Property(x => x.Email).HasMaxLength(200);
            builder.Property(x => x.Name).HasMaxLength(120);
            builder.Property(x => x.WorkType).HasConversion<int>();
            builder.Property(x => x.Status).HasConversion<int>();
            builder.Property(x => x.LastError).HasMaxLength(1000);

            builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);

            // El correo se guarda en minúsculas: un correo, un acceso (el handler además exige que sea de un solo cliente).
            builder.HasIndex(x => x.Email).IsUnique().HasFilter("\"IsDeleted\" = false");
            // El despachador busca los vencidos: estado + próxima hora de intento.
            builder.HasIndex(x => new { x.Status, x.NextAttemptOnUtc });
            builder.HasIndex(x => x.CustomerId);
        }
    }

    public class PortalAccessIssuerConfiguration : IEntityTypeConfiguration<PortalAccessIssuer>
    {
        public void Configure(EntityTypeBuilder<PortalAccessIssuer> builder)
        {
            builder.ToTable("PortalAccessIssuers", "Ops");
            builder.HasKey(x => new { x.PortalAccessId, x.IssuerId });

            builder.HasOne(x => x.PortalAccess).WithMany(a => a.Issuers).HasForeignKey(x => x.PortalAccessId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Issuer).WithMany().HasForeignKey(x => x.IssuerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => x.IssuerId);
        }
    }
}
