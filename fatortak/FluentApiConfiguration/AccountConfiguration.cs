using fatortak.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace fatortak.FluentApiConfiguration
{
    /// <summary>
    /// Fluent API configuration for Account entity.
    /// Defines relationships, indexes, and constraints.
    /// </summary>
    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            // Table name
            builder.ToTable("Accounts", "dbo");

            // Primary key
            builder.HasKey(a => a.Id);

            // Unique constraint: TenantId + AccountCode must be unique
            builder.HasIndex(a => new { a.TenantId, a.AccountCode })
                .IsUnique()
                .HasDatabaseName("IX_Accounts_TenantId_AccountCode");

            // Index for tenant queries
            builder.HasIndex(a => a.TenantId)
                .HasDatabaseName("IX_Accounts_TenantId");

            // Index for parent account queries
            builder.HasIndex(a => a.ParentAccountId)
                .HasDatabaseName("IX_Accounts_ParentAccountId");

            // Index for account type queries
            builder.HasIndex(a => new { a.TenantId, a.AccountType })
                .HasDatabaseName("IX_Accounts_TenantId_AccountType");

            // Index for active accounts
            builder.HasIndex(a => new { a.TenantId, a.IsActive })
                .HasDatabaseName("IX_Accounts_TenantId_IsActive");

            // Self-referencing relationship (parent-child)
            builder.HasOne(a => a.ParentAccount)
                .WithMany(a => a.ChildAccounts)
                .HasForeignKey(a => a.ParentAccountId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete to maintain data integrity

            // Tenant relationship
            builder.HasOne(a => a.Tenant)
                .WithMany()
                .HasForeignKey(a => a.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Property configurations
            builder.Property(a => a.AccountCode)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(a => a.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(a => a.Description)
                .HasMaxLength(1000);

            builder.Property(a => a.AccountType)
                .IsRequired()
                .HasConversion<int>(); // Store as int in database

            builder.Property(a => a.Level)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(a => a.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(a => a.IsPostable)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(a => a.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");
        }
    }
}

