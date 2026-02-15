using fatortak.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace fatortak.FluentApiConfiguration
{
    public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
    {
        public void Configure(EntityTypeBuilder<InvoiceItem> builder)
        {
            builder.HasOne(ii => ii.Tenant)
                .WithMany()
                .HasForeignKey(ii => ii.TenantId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(ii => ii.Invoice)
                .WithMany(i => i.InvoiceItems)
                .HasForeignKey(ii => ii.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade); // InvoiceItems should be deleted when Invoice is deleted
        }
    }
}
