using DS.Platform.Domain.Entities.Ops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DS.Platform.Infrastructure.Persistence.Contexts.Configurations.Ops
{
    public class ApiCredentialDeliveryConfiguration : IEntityTypeConfiguration<ApiCredentialDelivery>
    {
        public void Configure(EntityTypeBuilder<ApiCredentialDelivery> builder)
        {
            builder.ToTable("ApiCredentialDeliveries", "Ops");
            builder.Property(x => x.Ruc).HasMaxLength(11);
            builder.Property(x => x.Status).HasConversion<int>();
            builder.Property(x => x.LastError).HasMaxLength(1000);
            // Cifrados con ISecretProtector (dp1:...): solo mientras la entrega está Lista.
            builder.Property(x => x.ApiKeyProtected).HasMaxLength(2000);
            builder.Property(x => x.ApiSecretProtected).HasMaxLength(2000);

            builder.HasOne(x => x.Instance).WithMany().HasForeignKey(x => x.InstanceId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Issuer).WithMany().HasForeignKey(x => x.IssuerId).OnDelete(DeleteBehavior.Restrict);

            // Como mucho una entrega abierta por emisor: Pendiente (1), En curso (2), Lista (3) o Fallida (5).
            builder.HasIndex(x => x.IssuerId).IsUnique().HasFilter("\"IsDeleted\" = false AND \"Status\" IN (1, 2, 3, 5)");
            // El despachador busca las vencidas: estado + próxima hora de intento.
            builder.HasIndex(x => new { x.Status, x.NextAttemptOnUtc });
            // El ERP consulta las suyas por instancia y estado.
            builder.HasIndex(x => new { x.InstanceId, x.Status });
            builder.HasIndex(x => x.CustomerId);
        }
    }
}
