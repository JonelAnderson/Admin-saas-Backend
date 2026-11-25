using DS.Platform.Domain.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DS.Platform.Infrastructure.Persistence.Contexts.Configurations.Billing
{
    public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
    {
        public void Configure(EntityTypeBuilder<Subscription> builder)
        {
            builder.ToTable("Subscriptions", "Billing");
            builder.Property(x => x.Product).HasConversion<int>();
            builder.Property(x => x.BillingCycle).HasConversion<int>();
            builder.Property(x => x.Status).HasConversion<int>();
            builder.Property(x => x.Price).HasPrecision(12, 2);
            builder.Property(x => x.Currency).HasMaxLength(3);
            builder.Property(x => x.StatusReason).HasMaxLength(500);
            builder.Property(x => x.Notes).HasMaxLength(1000);

            builder.HasOne(x => x.Customer).WithMany(c => c.Subscriptions)
                .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Plan).WithMany()
                .HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);

            // Una sola suscripción no cancelada por cliente y producto (Status 5 = Cancelled).
            builder.HasIndex(x => new { x.CustomerId, x.Product }).IsUnique()
                .HasFilter("\"IsDeleted\" = false AND \"Status\" <> 5");
            // Job diario y tablero: suscripciones por estado y fecha de suspensión.
            builder.HasIndex(x => new { x.Status, x.SuspendOn });
            builder.HasIndex(x => x.PlanId);
        }
    }
}
