using DS.Platform.Domain.Entities.Ops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DS.Platform.Infrastructure.Persistence.Contexts.Configurations.Ops
{
    public class RemoteActionConfiguration : IEntityTypeConfiguration<RemoteAction>
    {
        public void Configure(EntityTypeBuilder<RemoteAction> builder)
        {
            builder.ToTable("RemoteActions", "Ops");
            builder.Property(x => x.Type).HasConversion<int>();
            builder.Property(x => x.Status).HasConversion<int>();
            builder.Property(x => x.Reason).HasMaxLength(500);
            builder.Property(x => x.IdempotencyKey).HasMaxLength(160);
            builder.Property(x => x.LastError).HasMaxLength(1000);
            builder.Property(x => x.PlanSlug).HasMaxLength(20);

            builder.HasOne(x => x.Issuer).WithMany().HasForeignKey(x => x.IssuerId).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => x.IdempotencyKey).IsUnique();
            // El despachador busca las vencidas: estado + próxima hora de intento.
            builder.HasIndex(x => new { x.Status, x.NextAttemptOnUtc });
            builder.HasIndex(x => x.IssuerId);
        }
    }
}
