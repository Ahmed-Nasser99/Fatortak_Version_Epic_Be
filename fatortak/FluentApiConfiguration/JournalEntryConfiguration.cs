using fatortak.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace fatortak.FluentApiConfiguration
{
    /// <summary>
    /// Fluent API configuration for JournalEntry entity.
    /// Defines relationships, indexes, and constraints.
    /// </summary>
    public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
    {
        public void Configure(EntityTypeBuilder<JournalEntry> builder)
        {
            // Table name
            builder.ToTable("JournalEntries", "dbo");

            // Primary key
            builder.HasKey(je => je.Id);

            // Unique constraint: TenantId + EntryNumber must be unique
            builder.HasIndex(je => new { je.TenantId, je.EntryNumber })
                .IsUnique()
                .HasDatabaseName("IX_JournalEntries_TenantId_EntryNumber");

            // Unique constraint: Prevent duplicate posting of same reference
            // Only one posted entry per ReferenceType + ReferenceId + TenantId
            builder.HasIndex(je => new { je.TenantId, je.ReferenceType, je.ReferenceId, je.IsPosted })
                .HasFilter("[IsPosted] = 1 AND [ReferenceId] IS NOT NULL")
                .HasDatabaseName("IX_JournalEntries_TenantId_ReferenceType_ReferenceId_IsPosted");

            // Index for tenant queries
            builder.HasIndex(je => je.TenantId)
                .HasDatabaseName("IX_JournalEntries_TenantId");

            // Index for date range queries
            builder.HasIndex(je => new { je.TenantId, je.Date })
                .HasDatabaseName("IX_JournalEntries_TenantId_Date");

            // Index for posted entries
            builder.HasIndex(je => new { je.TenantId, je.IsPosted })
                .HasDatabaseName("IX_JournalEntries_TenantId_IsPosted");

            // Index for reference queries
            builder.HasIndex(je => new { je.TenantId, je.ReferenceType, je.ReferenceId })
                .HasDatabaseName("IX_JournalEntries_TenantId_ReferenceType_ReferenceId");

            // Self-referencing relationship (reversing entries)
            builder.HasOne(je => je.ReversingEntry)
                .WithMany()
                .HasForeignKey(je => je.ReversingEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Tenant relationship
            builder.HasOne(je => je.Tenant)
                .WithMany()
                .HasForeignKey(je => je.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Property configurations
            builder.Property(je => je.EntryNumber)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(je => je.Date)
                .IsRequired()
                .HasColumnType("date");

            builder.Property(je => je.ReferenceType)
                .IsRequired()
                .HasConversion<int>(); // Store as int in database

            builder.Property(je => je.Description)
                .HasMaxLength(500);

            builder.Property(je => je.IsPosted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(je => je.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            // Relationship with lines
            builder.HasMany(je => je.Lines)
                .WithOne(jel => jel.JournalEntry)
                .HasForeignKey(jel => jel.JournalEntryId)
                .OnDelete(DeleteBehavior.Cascade); // Delete lines when entry is deleted
        }
    }
}

