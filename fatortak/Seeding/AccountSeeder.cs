using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Seeding
{
    /// <summary>
    /// Seeds default Chart of Accounts for each tenant
    /// </summary>
    public static class AccountSeeder
    {
        /// <summary>
        /// Seeds default accounts for all tenants or a specific tenant
        /// </summary>
        public static async Task SeedAccountsAsync(ApplicationDbContext context, Guid? tenantId = null)
        {
            var tenants = tenantId.HasValue
                ? await context.Tenants.Where(t => t.Id == tenantId.Value).ToListAsync()
                : await context.Tenants.ToListAsync();

            foreach (var tenant in tenants)
            {
                await SeedAccountsForTenantAsync(context, tenant.Id);
            }
        }

        /// <summary>
        /// Seeds default accounts for a specific tenant
        /// </summary>
        private static async Task SeedAccountsForTenantAsync(ApplicationDbContext context, Guid tenantId)
        {
            // Check if accounts already exist for this tenant
            var existingAccounts = await context.Accounts
                .Where(a => a.TenantId == tenantId)
                .AnyAsync();

            if (existingAccounts)
            {
                // Checks for specific missing accounts if tenant was created earlier
                var hasChequesAccount = await context.Accounts.AnyAsync(a => a.TenantId == tenantId && a.AccountCode == "1600");
                if (!hasChequesAccount)
                {
                    context.Accounts.Add(new Account
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        AccountCode = "1600",
                        Name = "Cheques Under Collection",
                        AccountType = AccountType.Asset,
                        Level = 0,
                        IsActive = true,
                        IsPostable = true,
                        IsSystem = true,
                        Description = "Cheques received from customers but not yet deposited",
                        CreatedAt = DateTime.UtcNow
                    });
                    await context.SaveChangesAsync();
                }

                return;
            }

            var accounts = new List<Account>();

            // Assets (1000-1999)
            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "1000",
                Name = "Cash",
                AccountType = AccountType.Asset,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Cash on hand",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "1100",
                Name = "Bank Account",
                AccountType = AccountType.Asset,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Bank accounts",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "1200",
                Name = "Accounts Receivable",
                AccountType = AccountType.Asset,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Amounts owed by customers",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "1300",
                Name = "VAT Input",
                AccountType = AccountType.Asset,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "VAT paid on purchases (recoverable)",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "1400",
                Name = "Inventory",
                AccountType = AccountType.Asset,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Inventory/Stock",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "1500",
                Name = "Employee Custody",
                AccountType = AccountType.Asset,
                Level = 0,
                IsActive = true,
                IsPostable = false, // Parent account for employee custody accounts
                IsSystem = true,
                Description = "Employee advances and custody accounts (parent account)",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "1600",
                Name = "Cheques Under Collection",
                AccountType = AccountType.Asset,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Cheques received from customers but not yet deposited",
                CreatedAt = DateTime.UtcNow
            });

            // Liabilities (2000-2999)
            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "2100",
                Name = "Accounts Payable",
                AccountType = AccountType.Liability,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Amounts owed to suppliers",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "2200",
                Name = "VAT Payable",
                AccountType = AccountType.Liability,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "VAT collected on sales (payable to tax authority)",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "2300",
                Name = "Accrued Expenses",
                AccountType = AccountType.Liability,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Accrued expenses",
                CreatedAt = DateTime.UtcNow
            });

            // Equity (3000-3999)
            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "3000",
                Name = "Capital",
                AccountType = AccountType.Equity,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Owner's capital",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "3100",
                Name = "Retained Earnings",
                AccountType = AccountType.Equity,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Accumulated profits",
                CreatedAt = DateTime.UtcNow
            });

            // Revenue (4000-4999)
            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "4000",
                Name = "Sales Revenue",
                AccountType = AccountType.Revenue,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Revenue from sales",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "4100",
                Name = "Service Revenue",
                AccountType = AccountType.Revenue,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Revenue from services",
                CreatedAt = DateTime.UtcNow
            });

            // Expenses (5000-5999)
            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "5000",
                Name = "General Expenses",
                AccountType = AccountType.Expense,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "General operating expenses",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "5100",
                Name = "Office Expenses",
                AccountType = AccountType.Expense,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Office supplies and expenses",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "5200",
                Name = "Transportation",
                AccountType = AccountType.Expense,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Transportation and travel expenses",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "5300",
                Name = "Raw Materials",
                AccountType = AccountType.Expense,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Cost of raw materials",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "5400",
                Name = "Utilities",
                AccountType = AccountType.Expense,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Utilities expenses (electricity, water, etc.)",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "5500",
                Name = "Rent",
                AccountType = AccountType.Expense,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Rent expenses",
                CreatedAt = DateTime.UtcNow
            });

            accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountCode = "5600",
                Name = "Salaries",
                AccountType = AccountType.Expense,
                Level = 0,
                IsActive = true,
                IsPostable = true,
                IsSystem = true,
                Description = "Salaries and wages",
                CreatedAt = DateTime.UtcNow
            });

            // Add all accounts to context
            context.Accounts.AddRange(accounts);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Seeds accounts for a new tenant (called when tenant is created)
        /// </summary>
        public static async Task SeedAccountsForNewTenantAsync(ApplicationDbContext context, Guid tenantId)
        {
            await SeedAccountsForTenantAsync(context, tenantId);
        }
    }
}

