using fatortak.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace fatortak.FluentApiConfiguration
{
    public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
    {
        public void Configure(EntityTypeBuilder<ApplicationUser> builder)
        {
            builder.HasOne(u => u.Tenant)
                .WithMany()
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.Cascade); // Keep cascade for main user-tenant relationship
        }
    }
}
