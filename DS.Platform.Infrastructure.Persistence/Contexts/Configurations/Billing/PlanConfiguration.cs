using DS.Platform.Domain.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DS.Platform.Infrastructure.Persistence.Contexts.Configurations.Billing
{
    public class PlanConfiguration : IEntityTypeConfiguration<Plan>
    {
        public void Configure(EntityTypeBuilder<Plan> builder)
        {
            builder.ToTable("Plans", "Billing");
            builder.Property(x => x.Product).HasConversion<int>();
            builder.Property(x => x.Code).HasMaxLength(32);
            builder.Property(x => x.Name).HasMaxLength(100);
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.Currency).HasMaxLength(3);
            builder.Property(x => x.ApiPlanSlug).HasMaxLength(20);
            builder.Property(x => x.MonthlyPrice).HasPrecision(12, 2);
            builder.Property(x => x.YearlyPrice).HasPrecision(12, 2);
            // Código único por producto entre los planes no borrados (un plan borrado libera su código).
            builder.HasIndex(x => new { x.Product, x.Code }).IsUnique().HasFilter("\"IsDeleted\" = false");
        }
    }
}
