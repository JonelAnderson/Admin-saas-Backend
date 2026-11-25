using DS.Platform.Domain.Entities.ApiSunat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DS.Platform.Infrastructure.Persistence.Contexts.Configurations.Customers
{
    public class ApiSunatIssuerConfiguration : IEntityTypeConfiguration<ApiSunatIssuer>
    {
        public void Configure(EntityTypeBuilder<ApiSunatIssuer> builder)
        {
            builder.ToTable("Issuers", "ApiSunat");
            builder.Property(x => x.Ruc).HasMaxLength(11);
            builder.Property(x => x.BusinessName).HasMaxLength(200);
            builder.Property(x => x.TradeName).HasMaxLength(200);
            builder.Property(x => x.Environment).HasMaxLength(20);
            builder.Property(x => x.ApiPlan).HasMaxLength(20);
            builder.Property(x => x.PlanDesiredSlug).HasMaxLength(20);
            builder.Property(x => x.Notes).HasMaxLength(1000);

            builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);

            // Una empresa de la API se vincula a un solo cliente, y un RUC una sola vez (entre los no borrados).
            builder.HasIndex(x => x.TenantId).IsUnique().HasFilter("\"IsDeleted\" = false");
            builder.HasIndex(x => x.Ruc).IsUnique().HasFilter("\"IsDeleted\" = false");
            builder.HasIndex(x => x.CustomerId);
        }
    }
}
