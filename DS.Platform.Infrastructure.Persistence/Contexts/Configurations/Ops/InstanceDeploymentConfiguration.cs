using DS.Platform.Domain.Entities.Ops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DS.Platform.Infrastructure.Persistence.Contexts.Configurations.Ops
{
    public class InstanceDeploymentConfiguration : IEntityTypeConfiguration<InstanceDeployment>
    {
        public void Configure(EntityTypeBuilder<InstanceDeployment> builder)
        {
            builder.ToTable("InstanceDeployments", "Ops");
            builder.Property(x => x.Type).HasConversion<int>();
            builder.Property(x => x.Step).HasConversion<int>();
            builder.Property(x => x.Status).HasConversion<int>();
            builder.Property(x => x.Version).HasMaxLength(40);
            builder.Property(x => x.LastError).HasMaxLength(1000);
            // Cifrado con ISecretProtector (dp1:...): lleva la clave de licencia, la del certificado y las contraseñas del stack.
            builder.Property(x => x.SecretsProtected).HasMaxLength(8000);

            builder.HasOne(x => x.Instance).WithMany().HasForeignKey(x => x.InstanceId).OnDelete(DeleteBehavior.Restrict);

            // El despachador busca los vencidos: estado + próxima hora de intento.
            builder.HasIndex(x => new { x.Status, x.NextAttemptOnUtc });
            // Como mucho un despliegue sin terminar por instancia (Pendiente, Enviando, Fallido o Abandonado).
            builder.HasIndex(x => x.InstanceId).IsUnique().HasFilter("\"IsDeleted\" = false AND \"Status\" IN (1, 2, 4, 5)");
        }
    }
}
