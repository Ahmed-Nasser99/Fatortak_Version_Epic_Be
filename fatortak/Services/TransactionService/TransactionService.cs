using fatortak.Context;
using fatortak.Dtos.Shared;
using fatortak.Dtos.Transaction;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.TransactionService
{
    public class TransactionService : ITransactionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TransactionService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TransactionService(
            ApplicationDbContext context,
            ILogger<TransactionService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private Guid TenantId => GetCurrentTenantId();

        private Guid GetCurrentTenantId()
        {
            var tenant = _httpContextAccessor.HttpContext?.Items["CurrentTenant"] as Tenant;
            return tenant?.Id ?? Guid.Empty;
        }

        public async Task<ServiceResult<Transaction>> AddTransactionAsync(Transaction transaction)
        {
            try
            {
                transaction.TenantId = TenantId;
                transaction.CreatedAt = DateTime.UtcNow;

                _context.Transactions.Add(transaction);

                // Update Balance Logic
                if (transaction.FinancialAccountId.HasValue)
                {
                    var account = await _context.FinancialAccounts.FindAsync(transaction.FinancialAccountId.Value);
                    if (account != null)
                    {
                        if (transaction.Direction == "Credit") // Money In
                        {
                            account.Balance += transaction.Amount;
                        }
                        else if (transaction.Direction == "Debit") // Money Out
                        {
                            account.Balance -= transaction.Amount;
                        }
                    }
                }

                if (transaction.CounterpartyAccountId.HasValue)
                {
                    var counterparty = await _context.FinancialAccounts.FindAsync(transaction.CounterpartyAccountId.Value);
                    if (counterparty != null)
                    {
                        // Assuming Transfer logic:
                        // Debit (Out) from Main -> Credit (In) to Counterparty
                        if (transaction.Direction == "Debit")
                        {
                            counterparty.Balance += transaction.Amount;
                        }
                        else if (transaction.Direction == "Credit") // Rare: Receiving transfer FROM counterparty?
                        {
                            counterparty.Balance -= transaction.Amount;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                return ServiceResult<Transaction>.SuccessResult(transaction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding transaction");
                return ServiceResult<Transaction>.Failure("Failed to add transaction");
            }
        }

        public async Task<ServiceResult<PagedResponseDto<Transaction>>> GetTransactionsAsync(TransactionFilterDto filter, PaginationDto pagination)
        {
            try
            {
                var query = _context.Transactions
                    .Include(t => t.Project)
                    .Include(t => t.FinancialAccount)
                    .Include(t => t.CounterpartyAccount)
                    .Where(t => t.TenantId == TenantId)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    var searchTerm = filter.Search.ToLower();
                    query = query.Where(t => 
                        (t.Description != null && t.Description.ToLower().Contains(searchTerm)) ||
                        (t.ReferenceType != null && t.ReferenceType.ToLower().Contains(searchTerm))
                    );
                }

                if (!string.IsNullOrWhiteSpace(filter.Type)) query = query.Where(t => t.Type == filter.Type);
                if (filter.FromDate.HasValue) query = query.Where(t => t.TransactionDate >= filter.FromDate.Value);
                if (filter.ToDate.HasValue) query = query.Where(t => t.TransactionDate <= filter.ToDate.Value);
                if (filter.MinAmount.HasValue) query = query.Where(t => t.Amount >= filter.MinAmount.Value);
                if (filter.MaxAmount.HasValue) query = query.Where(t => t.Amount <= filter.MaxAmount.Value);
                if (!string.IsNullOrEmpty(filter.ReferenceId)) query = query.Where(t => t.ReferenceId == filter.ReferenceId);
                if (!string.IsNullOrWhiteSpace(filter.ReferenceType)) query = query.Where(t => t.ReferenceType == filter.ReferenceType);
                
                // New Filters
                if (filter.ProjectId.HasValue) query = query.Where(t => t.ProjectId == filter.ProjectId);
                if (filter.FinancialAccountId.HasValue) query = query.Where(t => t.FinancialAccountId == filter.FinancialAccountId || t.CounterpartyAccountId == filter.FinancialAccountId);
                if (!string.IsNullOrWhiteSpace(filter.Category)) query = query.Where(t => t.Category == filter.Category);

                var totalCount = await query.CountAsync();

                var transactions = await query
                    .OrderByDescending(t => t.TransactionDate)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                return ServiceResult<PagedResponseDto<Transaction>>.SuccessResult(new PagedResponseDto<Transaction>
                {
                    Data = transactions,
                    PageNumber = pagination.PageNumber,
                    PageSize = pagination.PageSize,
                    TotalCount = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transactions");
                return ServiceResult<PagedResponseDto<Transaction>>.Failure("Failed to retrieve transactions");
            }
        }

        public async Task<ServiceResult<decimal>> GetBalanceAsync()
        {
            try
            {
                // This original method might be simplstic now that we have multiple accounts.
                // It likely summed ALL transactions. 
                // We should probably rely on FinancialAccount Balance now.
                // But keeping it for backward compatibility if needed, using the old logic:
                var balance = await _context.Transactions
                    .Where(t => t.TenantId == TenantId)
                    .SumAsync(t => t.Direction == "Credit" ? t.Amount : -t.Amount);

                return ServiceResult<decimal>.SuccessResult(balance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating balance");
                return ServiceResult<decimal>.Failure("Failed to calculate balance");
            }
         }
        public async Task<ServiceResult<bool>> DeleteTransactionByReferenceAsync(string referenceId, string referenceType)
        {
            try
            {
                var transaction = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.TenantId == TenantId && t.ReferenceId == referenceId && t.ReferenceType == referenceType);

                if (transaction == null)
                {
                    return ServiceResult<bool>.Failure("Transaction not found");
                }

                _context.Transactions.Remove(transaction);
                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting transaction by reference");
                return ServiceResult<bool>.Failure("Failed to delete transaction");
            }
        }

        public async Task<ServiceResult<Transaction>> UpdateTransactionByReferenceAsync(string referenceId, string referenceType, Transaction updatedTransaction)
        {
            try
            {
                var transaction = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.TenantId == TenantId && t.ReferenceId == referenceId && t.ReferenceType == referenceType);

                if (transaction == null)
                {
                    return ServiceResult<Transaction>.Failure("Transaction not found");
                }

                transaction.Amount = updatedTransaction.Amount;
                transaction.TransactionDate = updatedTransaction.TransactionDate;
                transaction.Description = updatedTransaction.Description;

                await _context.SaveChangesAsync();

                return ServiceResult<Transaction>.SuccessResult(transaction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating transaction by reference");
                return ServiceResult<Transaction>.Failure("Failed to update transaction");
            }
        }
    }
}
