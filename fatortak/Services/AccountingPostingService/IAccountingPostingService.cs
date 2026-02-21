namespace fatortak.Services.AccountingPostingService
{
    /// <summary>
    /// Service interface for posting business transactions to accounting journal entries.
    /// Converts invoices, expenses, and payments into double-entry journal entries.
    /// </summary>
    public interface IAccountingPostingService
    {
        /// <summary>
        /// Posts an invoice to accounting journal entries.
        /// For credit sales: Dr Accounts Receivable, Cr Sales Revenue, Cr VAT Payable
        /// </summary>
        Task<bool> PostInvoiceAsync(Guid invoiceId);

        /// <summary>
        /// Posts an expense to accounting journal entries.
        /// Dr Expense Account, Cr Cash/Bank Account
        /// </summary>
        Task<bool> PostExpenseAsync(int expenseId);

        /// <summary>
        /// Posts a customer payment to accounting journal entries.
        /// Dr Cash/Bank Account, Cr Accounts Receivable
        /// </summary>
        Task<bool> PostPaymentAsync(Guid invoiceId, decimal amount, Guid? transactionId = null, Guid? paymentAccountId = null, string? paymentMethod = null);

        /// <summary>
        /// Checks if an invoice has already been posted
        /// </summary>
        Task<bool> IsInvoicePostedAsync(Guid invoiceId);

        /// <summary>
        /// Checks if an expense has already been posted
        /// </summary>
        Task<bool> IsExpensePostedAsync(int expenseId);
    }
}

