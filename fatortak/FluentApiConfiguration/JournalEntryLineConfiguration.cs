using fatortak.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace fatortak.FluentApiConfiguration
{
    /// <summary>
    /// Fluent API configuration for JournalEntryLine entity.
    /// Defines relationships, indexes, and constraints.
    /// </summary>
    public class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
    {
        public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
        {
            // Table name
            builder.ToTable("JournalEntryLines", "dbo");

            // Primary key
            builder.HasKey(jel => jel.Id);

            // Index for journal entry queries
            builder.HasIndex(jel => jel.JournalEntryId)
                .HasDatabaseName("IX_JournalEntryLines_JournalEntryId");

            // Index for account queries (used in balance calculations)
            builder.HasIndex(jel => new { jel.AccountId, jel.JournalEntryId })
                .HasDatabaseName("IX_JournalEntryLines_AccountId_JournalEntryId");

            // Composite index for account balance queries (optimized for ledger queries)
            builder.HasIndex(jel => new { jel.AccountId })
                .HasDatabaseName("IX_JournalEntryLines_AccountId");

            // Relationship with journal entry
            builder.HasOne(jel => jel.JournalEntry)
                .WithMany(je => je.Lines)
                .HasForeignKey(jel => jel.JournalEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship with account
            builder.HasOne(jel => jel.Account)
                .WithMany(a => a.JournalEntryLines)
                .HasForeignKey(jel => jel.AccountId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deletion of accounts with entries

            // Property configurations
            builder.Property(jel => jel.Debit)
                .IsRequired()
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            builder.Property(jel => jel.Credit)
                .IsRequired()
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            builder.Property(jel => jel.Description)
                .HasMaxLength(500);

            builder.Property(jel => jel.Reference)
                .HasMaxLength(100);
        }
    }
}

