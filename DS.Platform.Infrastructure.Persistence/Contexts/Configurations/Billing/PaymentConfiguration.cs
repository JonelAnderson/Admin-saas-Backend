using DS.Platform.Domain.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DS.Platform.Infrastructure.Persistence.Contexts.Configurations.Billing
{
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payments", "Billing");
            builder.Property(x => x.Amount).HasPrecision(12, 2);
            builder.Property(x => x.ExpectedAmount).HasPrecision(12, 2);
            builder.Property(x => x.Currency).HasMaxLength(3);
            builder.Property(x => x.Method).HasConversion<int>();
            builder.Property(x => x.Status).HasConversion<int>();
            builder.Property(x => x.OperationNumber).HasMaxLength(60);
            builder.Property(x => x.VoidReason).HasMaxLength(400);
            builder.Property(x => x.Notes).HasMaxLength(1000);

            builder.HasOne(x => x.Subscription).WithMany()
                .HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Customer).WithMany()
                .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);

            // La misma operación bancaria no se registra dos veces (Status 1 = Registered; una anulada libera el número).
            builder.HasIndex(x => new { x.Method, x.OperationNumber }).IsUnique()
                .HasFilter("\"OperationNumber\" IS NOT NULL AND \"Status\" = 1 AND \"IsDeleted\" = false");
            builder.HasIndex(x => new { x.SubscriptionId, x.Status });
            builder.HasIndex(x => new { x.CustomerId, x.PaidOn });
            builder.HasIndex(x => x.PaidOn);
        }
    }
}
