using fatortak.Context;
using fatortak.Entities;
using fatortak.Services.TransactionService;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.BackfillService
{
    public interface IBackfillService
    {
        Task BackfillTransactionsAsync();
        Task BackfillBranchesAsync(Guid? tenantId = null);
    }

    public class BackfillService : IBackfillService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITransactionService _transactionService;
        private readonly ILogger<BackfillService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BackfillService(
            ApplicationDbContext context,
            ITransactionService transactionService,
            ILogger<BackfillService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _transactionService = transactionService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private Guid _tenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task BackfillTransactionsAsync()
        {
            try
            {
                // 1. Backfill Invoices (Paid/PartialPaid)
                var invoices = await _context.Invoices
                    .Where(i => i.TenantId == _tenantId && (i.Status == "Paid" || i.Status == "PartialPaid"))
                    .ToListAsync();

                foreach (var invoice in invoices)
                {
                    // Check if transaction already exists
                    var exists = await _context.Transactions
                        .AnyAsync(t => t.TenantId == _tenantId && t.ReferenceId == invoice.Id.ToString() && t.ReferenceType == "Invoice");

                    if (!exists)
                    {
                        var amount = invoice.AmountPaid.HasValue && invoice.AmountPaid > 0 
                            ? invoice.AmountPaid.Value 
                            : invoice.Total;

                        var transactionType = invoice.InvoiceType == "Sell" ? "PaymentReceived" : "PaymentMade";
                        var direction = invoice.InvoiceType == "Sell" ? "Credit" : "Debit";
                        var desc = invoice.InvoiceType == "Sell" ? "Backfilled payment received" : "Backfilled payment made";

                        await _transactionService.AddTransactionAsync(new Transaction
                        {
                            TenantId = invoice.TenantId,
                            TransactionDate = invoice.PaidAt ?? invoice.IssueDate,
                            Type = transactionType,
                            Amount = amount,
                            Direction = direction,
                            ReferenceId = invoice.Id.ToString(),
                            ReferenceType = "Invoice",
                            Description = $"{desc} for Invoice #{invoice.InvoiceNumber}",
                            PaymentMethod = "Cash",
                            CreatedBy = invoice.UserId
                        });
                    }
                }

                // 2. Backfill Expenses
                var expenses = await _context.Expenses
                    .Where(e => e.TenantId == _tenantId)
                    .ToListAsync();

                foreach (var expense in expenses)
                {
                    var exists = await _context.Transactions
                        .AnyAsync(t => t.TenantId == _tenantId && t.ReferenceId == expense.Id.ToString() && t.ReferenceType == "Expense");

                    if (!exists)
                    {
                        await _transactionService.AddTransactionAsync(new Transaction
                        {
                            TenantId = expense.TenantId,
                            TransactionDate = expense.Date.ToDateTime(TimeOnly.MinValue),
                            Type = "Expense",
                            Amount = expense.Total,
                            Direction = "Debit", // Expenses are Debits (money out)
                            ReferenceId = expense.Id.ToString(),
                            ReferenceType = "Expense",
                            Description = $"Backfilled expense: {expense.Notes}",
                            PaymentMethod = "Cash",
                            CreatedBy = null
                        });
                    }
                }

                _logger.LogInformation("Backfill completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during backfill");
                throw;
            }
        }

        public async Task BackfillBranchesAsync(Guid? tenantId = null)
        {
            try
            {
                var tenantsQuery = _context.Tenants.AsQueryable();
                if (tenantId.HasValue)
                {
                    tenantsQuery = tenantsQuery.Where(t => t.Id == tenantId.Value);
                }

                var tenants = await tenantsQuery.ToListAsync();
                foreach (var tenant in tenants)
                {
                    _logger.LogInformation($"Processing branch backfill for tenant: {tenant.Name} ({tenant.Id})");

                    // Check if tenant has any branch
                    var mainBranch = await _context.Branches
                        .FirstOrDefaultAsync(b => b.TenantId == tenant.Id && b.IsMain);

                    if (mainBranch == null)
                    {
                        // Create Main Branch
                        mainBranch = new Branch
                        {
                            TenantId = tenant.Id,
                            Name = "Main Branch",
                            IsMain = true,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Branches.Add(mainBranch);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Created Main Branch for tenant: {tenant.Name}");
                    }

                    // Update data that has null BranchId
                    var invoicesToUpdate = await _context.Invoices
                        .Where(i => i.TenantId == tenant.Id && i.BranchId == null)
                        .ToListAsync();
                    invoicesToUpdate.ForEach(i => i.BranchId = mainBranch.Id);

                    var itemsToUpdate = await _context.Items
                        .Where(i => i.TenantId == tenant.Id && i.BranchId == null)
                        .ToListAsync();
                    itemsToUpdate.ForEach(i => i.BranchId = mainBranch.Id);

                    var expensesToUpdate = await _context.Expenses
                        .Where(e => e.TenantId == tenant.Id && e.BranchId == null)
                        .ToListAsync();
                    expensesToUpdate.ForEach(e => e.BranchId = mainBranch.Id);

                    var transactionsToUpdate = await _context.Transactions
                        .Where(t => t.TenantId == tenant.Id && t.BranchId == null)
                        .ToListAsync();
                    transactionsToUpdate.ForEach(t => t.BranchId = mainBranch.Id);

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Updated data for tenant: {tenant.Name}. Invoices: {invoicesToUpdate.Count}, Items: {itemsToUpdate.Count}, Expenses: {expensesToUpdate.Count}, Transactions: {transactionsToUpdate.Count}");
                }

                _logger.LogInformation("Branch backfill completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during branch backfill");
                throw;
            }
        }
    }
}
