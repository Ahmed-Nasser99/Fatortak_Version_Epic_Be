using fatortak.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace fatortak.FluentApiConfiguration
{
    public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.HasOne(t => t.FinancialAccount)
                .WithMany(fa => fa.Transactions)
                .HasForeignKey(t => t.FinancialAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(t => t.CounterpartyAccount)
                .WithMany() // No navigation property needed on FinancialAccount for counterparty transactions strictly
                .HasForeignKey(t => t.CounterpartyAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(t => t.Project)
                .WithMany(p => p.Transactions)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
