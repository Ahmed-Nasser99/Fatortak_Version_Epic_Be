using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Dtos.Cheque;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using fatortak.Helpers;
using fatortak.Services.AccountingPostingService;
using fatortak.Services.TransactionService;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.ChequeService
{
    public class ChequeService : IChequeService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ChequeService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ITransactionService _transactionService;
        private readonly IAccountingPostingService _accountingPostingService;

        public ChequeService(ApplicationDbContext context, ILogger<ChequeService> logger, IHttpContextAccessor httpContextAccessor, ITransactionService transactionService, IAccountingPostingService accountingPostingService)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _transactionService = transactionService;
            _accountingPostingService = accountingPostingService;
        }

        private Guid TenantId => GetCurrentTenantId();

        private Guid GetCurrentTenantId()
        {
            var tenant = _httpContextAccessor.HttpContext?.Items["CurrentTenant"] as Tenant;
            return tenant?.Id ?? Guid.Empty;
        }

        public async Task<ServiceResult<PagedResponseDto<ChequeDto>>> GetChequesAsync(PaginationDto pagination, string? status = null)
        {
            try
            {
                var query = _context.Cheques
                    .Include(c => c.Invoice)
                        .ThenInclude(i => i.Project)
                    .Include(c => c.PaymentAccount)
                    .Where(c => c.TenantId == TenantId)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(c => c.Status == status);
                }

                var totalCount = await query.CountAsync();
                var cheques = await query
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .Select(c => new ChequeDto
                    {
                        Id = c.Id,
                        ChequeNumber = c.ChequeNumber,
                        BankName = c.BankName,
                        DueDate = c.DueDate,
                        Amount = c.Amount,
                        Status = c.Status,
                        InvoiceId = c.InvoiceId,
                        InvoiceNumber = c.Invoice.InvoiceNumber,
                        ProjectName = c.Invoice.Project != null ? c.Invoice.Project.Name : null,
                        PaymentAccountId = c.PaymentAccountId,
                        PaymentAccountName = c.PaymentAccount != null ? c.PaymentAccount.Name : null,
                        CreatedAt = c.CreatedAt
                    })
                    .ToListAsync();

                return ServiceResult<PagedResponseDto<ChequeDto>>.SuccessResult(new PagedResponseDto<ChequeDto>
                {
                    Data = cheques,
                    PageNumber = pagination.PageNumber,
                    PageSize = pagination.PageSize,
                    TotalCount = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cheques");
                return ServiceResult<PagedResponseDto<ChequeDto>>.Failure("Failed to retrieve cheques");
            }
        }

        public async Task<ServiceResult<ChequeDto>> UpdateChequeStatusAsync(Guid chequeId, UpdateChequeStatusDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var cheque = await _context.Cheques
                    .Include(c => c.Invoice)
                    .ThenInclude(i => i.Customer) // needed if we revert status and need lastengagement date, or just good to have
                    .FirstOrDefaultAsync(c => c.Id == chequeId && c.TenantId == TenantId);

                if (cheque == null)
                    return ServiceResult<ChequeDto>.Failure("Cheque not found");

                if (cheque.Status == dto.Status)
                    return ServiceResult<ChequeDto>.Failure($"Cheque is already {dto.Status}");

                var previousStatus = cheque.Status;
                cheque.Status = dto.Status;
                cheque.UpdatedAt = DateTime.UtcNow;

                var invoice = cheque.Invoice;

                // Handle transitioning to Deposited
                if (dto.Status == ChequeStatus.Deposited.ToString() && previousStatus == ChequeStatus.UnderCollection.ToString())
                {
                    // Debit Bank (PaymentAccountId), Credit Cheque Under Collection
                    var paymentPosted = await _accountingPostingService.PostPaymentAsync(
                        invoice.Id,
                        cheque.Amount,
                        cheque.Id,
                        cheque.PaymentAccountId,
                        "Cheque_Deposited");

                    if (!paymentPosted)
                    {
                        _logger.LogWarning("Failed to post accounting entry for cheque deposit");
                    }

                    // Check if invoice is fully paid to update status from PartPaid/AwaitingChequeClearance to Paid
                    if (invoice.AmountPaid >= invoice.Total)
                    {
                        invoice.Status = InvoiceStatus.Paid.ToString();
                        invoice.PaidAt = DateTime.UtcNow;
                    }

                }
                // Handle transitioning to Bounced
                else if (dto.Status == ChequeStatus.Bounced.ToString() && previousStatus == ChequeStatus.UnderCollection.ToString())
                {
                    // Reverse the original Accounts Receivable vs Cheques Under Collection entry
                    // We'll debit AR again, and Credit Cheques Under Collection
                    var bouncePosted = await _accountingPostingService.PostPaymentAsync(
                        invoice.Id,
                        cheque.Amount,
                        cheque.Id,
                        cheque.PaymentAccountId,
                        "Cheque_Bounced");

                    if (!bouncePosted)
                    {
                        _logger.LogWarning("Failed to post accounting entry for bounced cheque");
                    }

                    // Revert the amount paid
                    invoice.AmountPaid -= cheque.Amount;

                    // Recalculate invoice status
                    if (invoice.AmountPaid <= 0)
                    {
                        invoice.AmountPaid = 0;
                        invoice.Status = InvoiceStatus.Pending.ToString();
                    }
                    else if (invoice.Installments != null && invoice.Installments.Any())
                    {
                        invoice.Status = InvoiceStatus.PartialPaid.ToString();
                    }
                    else
                    {
                        invoice.Status = InvoiceStatus.PartPaid.ToString();
                    }

                    // Cancel the related transaction if it exists
                    if (cheque.TransactionId.HasValue)
                    {
                        var paymentTransaction = await _context.Transactions.FindAsync(cheque.TransactionId.Value);
                        if (paymentTransaction != null)
                        {
                            paymentTransaction.Description += " (Cancelled - Cheque Bounced)";
                            // Conceptually reversing it via a new transaction makes more sense in double-entry,
                            // but for simplicity we'll just add a reversing transaction
                            var userIdString = _httpContextAccessor.HttpContext?.User?.FindFirst("UserId")?.Value;
                            var userId = !string.IsNullOrEmpty(userIdString) ? Guid.Parse(userIdString) : (Guid?)null;

                            await _transactionService.AddTransactionAsync(new Transaction
                            {
                                TenantId = cheque.TenantId,
                                TransactionDate = DateTime.UtcNow,
                                Type = paymentTransaction.Type == "PaymentReceived" ? "PaymentMade" : "PaymentReceived",
                                Amount = cheque.Amount,
                                Direction = paymentTransaction.Direction == "Credit" ? "Debit" : "Credit",
                                ReferenceId = invoice.Id.ToString(),
                                ReferenceType = "Invoice_Cheque_Bounce",
                                Description = $"Reversal of Transaction #{paymentTransaction.Id} due to Bounced Cheque",
                                PaymentMethod = "Cheque",
                                CreatedBy = userId,
                                BranchId = paymentTransaction.BranchId,
                                ProjectId = paymentTransaction.ProjectId
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult<ChequeDto>.SuccessResult(new ChequeDto
                {
                    Id = cheque.Id,
                    ChequeNumber = cheque.ChequeNumber,
                    BankName = cheque.BankName,
                    DueDate = cheque.DueDate,
                    Amount = cheque.Amount,
                    Status = cheque.Status,
                    InvoiceId = cheque.InvoiceId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    PaymentAccountId = cheque.PaymentAccountId,
                    CreatedAt = cheque.CreatedAt
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating cheque status");
                return ServiceResult<ChequeDto>.Failure("Failed to update cheque status");
            }
        }
    }
}
