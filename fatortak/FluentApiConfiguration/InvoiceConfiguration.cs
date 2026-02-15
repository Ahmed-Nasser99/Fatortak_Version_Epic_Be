using fatortak.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace fatortak.FluentApiConfiguration
{
    public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> builder)
        {
            builder.HasOne(i => i.Tenant)
                .WithMany()
                .HasForeignKey(i => i.TenantId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
