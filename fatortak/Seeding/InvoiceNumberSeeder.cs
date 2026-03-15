using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace fatortak.Seeding
{
    public static class InvoiceNumberSeeder
    {
        public static async Task FixInvoiceNumbersAsync(ApplicationDbContext context)
        {
            var tenants = await context.Tenants.ToListAsync();

            foreach (var tenant in tenants)
            {
                var company = await context.Companies.FirstOrDefaultAsync(c => c.TenantId == tenant.Id);
                string prefix = company?.InvoicePrefix ?? "INV-";

                // Retrieve all invoices for this tenant, ordered by creation date
                var invoices = await context.Invoices
                    .Where(i => i.TenantId == tenant.Id)
                    .OrderBy(i => i.CreatedAt)
                    .ToListAsync();

                // Group by normalized InvoiceType (e.g., selling vs buying)
                var groupedByType = invoices.GroupBy(i => i.InvoiceType?.ToLower() ?? InvoiceTypes.Sell.ToString().ToLower());

                foreach (var group in groupedByType)
                {
                    long counter = 1;
                    foreach (var invoice in group)
                    {
                        // Generate the correct sequence number like INV-0001
                        invoice.InvoiceNumber = $"{prefix}{counter.ToString().PadLeft(4, '0')}";
                        counter++;
                    }
                }
            }

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync();
            }
        }
    }
}
