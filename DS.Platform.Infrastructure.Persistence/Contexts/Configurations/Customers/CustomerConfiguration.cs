using DS.Platform.Domain.Entities.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DS.Platform.Infrastructure.Persistence.Contexts.Configurations.Customers
{
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            builder.ToTable("Customers", "Core");
            builder.Property(x => x.Ruc).HasMaxLength(11);
            builder.Property(x => x.BusinessName).HasMaxLength(200);
            builder.Property(x => x.TradeName).HasMaxLength(200);
            builder.Property(x => x.Address).HasMaxLength(300);
            builder.Property(x => x.ContactName).HasMaxLength(150);
            builder.Property(x => x.ContactEmail).HasMaxLength(150);
            builder.Property(x => x.ContactPhone).HasMaxLength(20);
            builder.Property(x => x.Notes).HasMaxLength(1000);
            // RUC único entre los clientes no borrados.
            builder.HasIndex(x => x.Ruc).IsUnique().HasFilter("\"IsDeleted\" = false");
        }
    }
}
