using fatortak.Dtos.Accounting;
using fatortak.Dtos.Shared;
using fatortak.Services.AccountingService;
using fatortak.Services.AccountingPostingService;
using fatortak.Services.CustodyService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    /// <summary>
    /// Controller for accounting operations including Chart of Accounts, Journal Entries, and Financial Reports
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AccountingController : ControllerBase
    {
        private readonly IAccountingService _accountingService;
        private readonly IAccountingPostingService _postingService;
        private readonly ICustodyService _custodyService;
        private readonly ILogger<AccountingController> _logger;

        public AccountingController(
            IAccountingService accountingService,
            IAccountingPostingService postingService,
            ICustodyService custodyService,
            ILogger<AccountingController> logger)
        {
            _accountingService = accountingService;
            _postingService = postingService;
            _custodyService = custodyService;
            _logger = logger;
        }

        #region Account Management

        /// <summary>
        /// Create a new account in the Chart of Accounts
        /// </summary>
        [HttpPost("accounts")]
        public async Task<ActionResult<ServiceResult<AccountDto>>> CreateAccount([FromBody] AccountCreateDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(ServiceResult<AccountDto>.ValidationError(errors));
                }

                var result = await _accountingService.CreateAccountAsync(dto);
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account");
                return StatusCode(500, ServiceResult<AccountDto>.Failure("Failed to create account"));
            }
        }

        /// <summary>
        /// Get account by ID
        /// </summary>
        [HttpGet("accounts/{accountId}")]
        public async Task<ActionResult<ServiceResult<AccountDto>>> GetAccount(Guid accountId)
        {
            try
            {
                var result = await _accountingService.GetAccountAsync(accountId);
                if (!result.Success)
                {
                    if (result.ErrorMessage == "Account not found")
                    {
                        return NotFound(result);
                    }
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting account {accountId}");
                return StatusCode(500, ServiceResult<AccountDto>.Failure("Failed to get account"));
            }
        }

        /// <summary>
        /// Get accounts with filtering and pagination
        /// </summary>
        [HttpGet("accounts")]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<AccountDto>>>> GetAccounts(
            [FromQuery] AccountFilterDto filter,
            [FromQuery] PaginationDto pagination)
        {
            try
            {
                var result = await _accountingService.GetAccountsAsync(filter, pagination);
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accounts");
                return StatusCode(500, ServiceResult<PagedResponseDto<AccountDto>>.Failure("Failed to get accounts"));
            }
        }

        /// <summary>
        /// Get account hierarchy (tree structure)
        /// </summary>
        [HttpGet("accounts/hierarchy")]
        public async Task<ActionResult<ServiceResult<List<AccountDto>>>> GetAccountHierarchy()
        {
            try
            {
                var result = await _accountingService.GetAccountHierarchyAsync();
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account hierarchy");
                return StatusCode(500, ServiceResult<List<AccountDto>>.Failure("Failed to get account hierarchy"));
            }
        }

        /// <summary>
        /// Update an account
        /// </summary>
        [HttpPost("accounts/{accountId}/update")]
        public async Task<ActionResult<ServiceResult<AccountDto>>> UpdateAccount(
            Guid accountId,
            [FromBody] AccountUpdateDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(ServiceResult<AccountDto>.ValidationError(errors));
                }

                var result = await _accountingService.UpdateAccountAsync(accountId, dto);
                if (!result.Success)
                {
                    if (result.ErrorMessage == "Account not found")
                    {
                        return NotFound(result);
                    }
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating account {accountId}");
                return StatusCode(500, ServiceResult<AccountDto>.Failure("Failed to update account"));
            }
        }

        /// <summary>
        /// Delete an account (only if no journal entries exist)
        /// </summary>
        [HttpPost("accounts/{accountId}/delete")]
        public async Task<ActionResult<ServiceResult<bool>>> DeleteAccount(Guid accountId)
        {
            try
            {
                var result = await _accountingService.DeleteAccountAsync(accountId);
                if (!result.Success)
                {
                    if (result.ErrorMessage == "Account not found")
                    {
                        return NotFound(result);
                    }
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting account {accountId}");
                return StatusCode(500, ServiceResult<bool>.Failure("Failed to delete account"));
            }
        }

        #endregion

        #region Journal Entry Management

        /// <summary>
        /// Create a manual journal entry
        /// </summary>
        [HttpPost("journal-entries")]
        public async Task<ActionResult<ServiceResult<JournalEntryDto>>> CreateJournalEntry([FromBody] JournalEntryCreateDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(ServiceResult<JournalEntryDto>.ValidationError(errors));
                }

                var result = await _accountingService.CreateManualJournalEntryAsync(dto);
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating journal entry");
                return StatusCode(500, ServiceResult<JournalEntryDto>.Failure("Failed to create journal entry"));
            }
        }

        /// <summary>
        /// Get journal entry by ID
        /// </summary>
        [HttpGet("journal-entries/{journalEntryId}")]
        public async Task<ActionResult<ServiceResult<JournalEntryDto>>> GetJournalEntry(Guid journalEntryId)
        {
            try
            {
                var result = await _accountingService.GetJournalEntryAsync(journalEntryId);
                if (!result.Success)
                {
                    if (result.ErrorMessage == "Journal entry not found")
                    {
                        return NotFound(result);
                    }
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting journal entry {journalEntryId}");
                return StatusCode(500, ServiceResult<JournalEntryDto>.Failure("Failed to get journal entry"));
            }
        }

        /// <summary>
        /// Get journal entries with filtering and pagination
        /// </summary>
        [HttpGet("journal-entries")]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<JournalEntryDto>>>> GetJournalEntries(
            [FromQuery] JournalEntryFilterDto filter,
            [FromQuery] PaginationDto pagination)
        {
            try
            {
                var result = await _accountingService.GetJournalEntriesAsync(filter, pagination);
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting journal entries");
                return StatusCode(500, ServiceResult<PagedResponseDto<JournalEntryDto>>.Failure("Failed to get journal entries"));
            }
        }

        /// <summary>
        /// Post a journal entry (finalize it)
        /// </summary>
        [HttpPost("journal-entries/{journalEntryId}/post")]
        public async Task<ActionResult<ServiceResult<bool>>> PostJournalEntry(Guid journalEntryId)
        {
            try
            {
                var result = await _accountingService.PostJournalEntryAsync(journalEntryId);
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error posting journal entry {journalEntryId}");
                return StatusCode(500, ServiceResult<bool>.Failure("Failed to post journal entry"));
            }
        }

        /// <summary>
        /// Reverse a posted journal entry
        /// </summary>
        [HttpPost("journal-entries/{journalEntryId}/reverse")]
        public async Task<ActionResult<ServiceResult<bool>>> ReverseJournalEntry(Guid journalEntryId)
        {
            try
            {
                var result = await _accountingService.ReverseJournalEntryAsync(journalEntryId);
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reversing journal entry {journalEntryId}");
                return StatusCode(500, ServiceResult<bool>.Failure("Failed to reverse journal entry"));
            }
        }

        #endregion

        #region General Ledger Queries

        /// <summary>
        /// Get account balance as of a specific date
        /// </summary>
        [HttpGet("accounts/{accountId}/balance")]
        public async Task<ActionResult<ServiceResult<AccountBalanceDto>>> GetAccountBalance(
            Guid accountId,
            [FromQuery] DateTime? asOfDate = null)
        {
            try
            {
                var result = await _accountingService.GetAccountBalanceAsync(accountId, asOfDate);
                if (!result.Success)
                {
                    if (result.ErrorMessage == "Account not found")
                    {
                        return NotFound(result);
                    }
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting account balance for {accountId}");
                return StatusCode(500, ServiceResult<AccountBalanceDto>.Failure("Failed to get account balance"));
            }
        }

        /// <summary>
        /// Get trial balance as of a specific date
        /// </summary>
        [HttpGet("reports/trial-balance")]
        public async Task<ActionResult<ServiceResult<TrialBalanceDto>>> GetTrialBalance([FromQuery] DateTime? asOfDate = null)
        {
            try
            {
                var result = await _accountingService.GetTrialBalanceAsync(asOfDate);
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting trial balance");
                return StatusCode(500, ServiceResult<TrialBalanceDto>.Failure("Failed to get trial balance"));
            }
        }

        /// <summary>
        /// Get account ledger (running balance) for a date range
        /// </summary>
        [HttpGet("accounts/{accountId}/ledger")]
        public async Task<ActionResult<ServiceResult<LedgerDto>>> GetAccountLedger(
            Guid accountId,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var result = await _accountingService.GetAccountLedgerAsync(accountId, fromDate, toDate);
                if (!result.Success)
                {
                    if (result.ErrorMessage == "Account not found")
                    {
                        return NotFound(result);
                    }
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting account ledger for {accountId}");
                return StatusCode(500, ServiceResult<LedgerDto>.Failure("Failed to get account ledger"));
            }
        }

        /// <summary>
        /// Get Profit & Loss (Income Statement) report
        /// </summary>
        [HttpGet("reports/profit-and-loss")]
        public async Task<ActionResult<ServiceResult<ProfitAndLossDto>>> GetProfitAndLoss(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate)
        {
            try
            {
                var result = await _accountingService.GetProfitAndLossAsync(fromDate, toDate);
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting profit and loss");
                return StatusCode(500, ServiceResult<ProfitAndLossDto>.Failure("Failed to get profit and loss"));
            }
        }

        /// <summary>
        /// Get Balance Sheet report
        /// </summary>
        [HttpGet("reports/balance-sheet")]
        public async Task<ActionResult<ServiceResult<BalanceSheetDto>>> GetBalanceSheet([FromQuery] DateTime asOfDate)
        {
            try
            {
                var result = await _accountingService.GetBalanceSheetAsync(asOfDate);
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting balance sheet");
                return StatusCode(500, ServiceResult<BalanceSheetDto>.Failure("Failed to get balance sheet"));
            }
        }

        #endregion

        #region Posting Operations

        /// <summary>
        /// Post an invoice to accounting journal entries
        /// </summary>
        [HttpPost("posting/invoice/{invoiceId}")]
        public async Task<ActionResult<bool>> PostInvoice(Guid invoiceId)
        {
            try
            {
                var result = await _postingService.PostInvoiceAsync(invoiceId);
                if (!result)
                {
                    return BadRequest(new { message = "Failed to post invoice. It may have already been posted or required accounts are missing." });
                }

                return Ok(new { success = true, message = "Invoice posted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error posting invoice {invoiceId}");
                return StatusCode(500, new { message = "Failed to post invoice" });
            }
        }

        /// <summary>
        /// Post an expense to accounting journal entries
        /// </summary>
        [HttpPost("posting/expense/{expenseId}")]
        public async Task<ActionResult<bool>> PostExpense(int expenseId)
        {
            try
            {
                var result = await _postingService.PostExpenseAsync(expenseId);
                if (!result)
                {
                    return BadRequest(new { message = "Failed to post expense. It may have already been posted or required accounts are missing." });
                }

                return Ok(new { success = true, message = "Expense posted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error posting expense {expenseId}");
                return StatusCode(500, new { message = "Failed to post expense" });
            }
        }

        /// <summary>
        /// Post a customer payment to accounting journal entries
        /// </summary>
        [HttpPost("posting/payment")]
        public async Task<ActionResult<bool>> PostPayment(
            [FromBody] PostPaymentDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _postingService.PostPaymentAsync(dto.InvoiceId, dto.Amount);
                if (!result)
                {
                    return BadRequest(new { message = "Failed to post payment. It may have already been posted or required accounts are missing." });
                }

                return Ok(new { success = true, message = "Payment posted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error posting payment for invoice {dto.InvoiceId}");
                return StatusCode(500, new { message = "Failed to post payment" });
            }
        }

        /// <summary>
        /// Check if an invoice has been posted
        /// </summary>
        [HttpGet("posting/invoice/{invoiceId}/status")]
        public async Task<ActionResult<bool>> GetInvoicePostingStatus(Guid invoiceId)
        {
            try
            {
                var isPosted = await _postingService.IsInvoicePostedAsync(invoiceId);
                return Ok(new { invoiceId, isPosted });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking invoice posting status {invoiceId}");
                return StatusCode(500, new { message = "Failed to check posting status" });
            }
        }

        /// <summary>
        /// Check if an expense has been posted
        /// </summary>
        [HttpGet("posting/expense/{expenseId}/status")]
        public async Task<ActionResult<bool>> GetExpensePostingStatus(int expenseId)
        {
            try
            {
                var isPosted = await _postingService.IsExpensePostedAsync(expenseId);
                return Ok(new { expenseId, isPosted });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking expense posting status {expenseId}");
                return StatusCode(500, new { message = "Failed to check posting status" });
            }
        }

        #endregion

        #region Custody Operations

        /// <summary>
        /// Give custody (advance) to an employee
        /// </summary>
        [HttpPost("custody/give")]
        public async Task<ActionResult<bool>> GiveCustody([FromBody] GiveCustodyDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _custodyService.GiveCustodyAsync(
                    dto.EmployeeId,
                    dto.Amount,
                    dto.SourceAccountId,
                    dto.Description);

                if (!result)
                {
                    return BadRequest(new { message = "Failed to give custody. Check logs for details." });
                }

                return Ok(new { success = true, message = "Custody given successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error giving custody to employee {dto.EmployeeId}");
                return StatusCode(500, new { message = "Failed to give custody" });
            }
        }

        /// <summary>
        /// Use custody for an expense
        /// </summary>
        [HttpPost("custody/use-for-expense")]
        public async Task<ActionResult<bool>> UseCustodyForExpense([FromBody] UseCustodyForExpenseDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _custodyService.UseCustodyForExpenseAsync(
                    dto.ExpenseId,
                    dto.EmployeeId);

                if (!result)
                {
                    return BadRequest(new { message = "Failed to use custody for expense. Check logs for details." });
                }

                return Ok(new { success = true, message = "Expense posted using custody successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error using custody for expense {dto.ExpenseId}");
                return StatusCode(500, new { message = "Failed to use custody for expense" });
            }
        }

        /// <summary>
        /// Return custody (unused advance) from employee
        /// </summary>
        [HttpPost("custody/return")]
        public async Task<ActionResult<bool>> ReturnCustody([FromBody] ReturnCustodyDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _custodyService.ReturnCustodyAsync(
                    dto.EmployeeId,
                    dto.Amount,
                    dto.DestinationAccountId,
                    dto.Description);

                if (!result)
                {
                    return BadRequest(new { message = "Failed to return custody. Check logs for details." });
                }

                return Ok(new { success = true, message = "Custody returned successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error returning custody from employee {dto.EmployeeId}");
                return StatusCode(500, new { message = "Failed to return custody" });
            }
        }

        /// <summary>
        /// Replenish custody (add more money to employee's advance)
        /// </summary>
        [HttpPost("custody/replenish")]
        public async Task<ActionResult<bool>> ReplenishCustody([FromBody] ReplenishCustodyDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _custodyService.ReplenishCustodyAsync(
                    dto.EmployeeId,
                    dto.Amount,
                    dto.SourceAccountId,
                    dto.Description);

                if (!result)
                {
                    return BadRequest(new { message = "Failed to replenish custody. Check logs for details." });
                }

                return Ok(new { success = true, message = "Custody replenished successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error replenishing custody for employee {dto.EmployeeId}");
                return StatusCode(500, new { message = "Failed to replenish custody" });
            }
        }

        /// <summary>
        /// Get employee custody balance
        /// </summary>
        [HttpGet("custody/balance/{employeeId}")]
        public async Task<ActionResult<decimal>> GetEmployeeCustodyBalance(Guid employeeId)
        {
            try
            {
                var balance = await _custodyService.GetEmployeeCustodyBalanceAsync(employeeId);
                return Ok(new { employeeId, balance });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting custody balance for employee {employeeId}");
                return StatusCode(500, new { message = "Failed to get custody balance" });
            }
        }

        #endregion
    }

    /// <summary>
    /// DTO for posting payment
    /// </summary>
    public class PostPaymentDto
    {
        public Guid InvoiceId { get; set; }
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// DTO for giving custody
    /// </summary>
    public class GiveCustodyDto
    {
        public Guid EmployeeId { get; set; }
        public decimal Amount { get; set; }
        public Guid? SourceAccountId { get; set; } // Cash/Bank account ID from Chart of Accounts
        public string? Description { get; set; }
    }

    /// <summary>
    /// DTO for using custody for expense
    /// </summary>
    public class UseCustodyForExpenseDto
    {
        public int ExpenseId { get; set; }
        public Guid EmployeeId { get; set; }
    }

    /// <summary>
    /// DTO for returning custody
    /// </summary>
    public class ReturnCustodyDto
    {
        public Guid EmployeeId { get; set; }
        public decimal Amount { get; set; }
        public Guid? DestinationAccountId { get; set; } // Cash/Bank account ID from Chart of Accounts
        public string? Description { get; set; }
    }

    /// <summary>
    /// DTO for replenishing custody
    /// </summary>
    public class ReplenishCustodyDto
    {
        public Guid EmployeeId { get; set; }
        public decimal Amount { get; set; }
        public Guid? SourceAccountId { get; set; } // Cash/Bank account ID from Chart of Accounts
        public string? Description { get; set; }
    }
}

