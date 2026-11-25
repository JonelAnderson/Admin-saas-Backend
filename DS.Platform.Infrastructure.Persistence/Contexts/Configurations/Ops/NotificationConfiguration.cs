using DS.Platform.Domain.Entities.Ops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DS.Platform.Infrastructure.Persistence.Contexts.Configurations.Ops
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notifications", "Ops");
            builder.Property(x => x.Type).HasConversion<int>();
            builder.Property(x => x.Status).HasConversion<int>();
            builder.Property(x => x.DedupKey).HasMaxLength(200);
            builder.Property(x => x.ToEmail).HasMaxLength(200);
            builder.Property(x => x.Subject).HasMaxLength(300);
            builder.Property(x => x.Body).HasColumnType("text");
            builder.Property(x => x.LastError).HasMaxLength(1000);

            builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);

            // El mismo aviso del mismo periodo no se guarda dos veces.
            builder.HasIndex(x => x.DedupKey).IsUnique();
            builder.HasIndex(x => new { x.Status, x.NextAttemptOnUtc });
            builder.HasIndex(x => x.CustomerId);
        }
    }
}
