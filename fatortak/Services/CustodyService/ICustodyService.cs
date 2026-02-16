namespace fatortak.Services.CustodyService
{
    /// <summary>
    /// Service for handling employee custody (advances) using Chart of Accounts.
    /// Custody is tracked as an Asset account in the accounting system.
    /// </summary>
    public interface ICustodyService
    {
        /// <summary>
        /// Give custody to an employee (advance payment).
        /// Creates: Dr Employee Custody Account, Cr Cash/Bank Account
        /// </summary>
        Task<bool> GiveCustodyAsync(Guid employeeId, decimal amount, Guid? sourceAccountId, string? description);

        /// <summary>
        /// Use custody for an expense (employee uses their advance).
        /// Creates: Dr Expense Account, Cr Employee Custody Account
        /// </summary>
        Task<bool> UseCustodyForExpenseAsync(int expenseId, Guid employeeId);

        /// <summary>
        /// Return custody (employee returns unused advance).
        /// Creates: Dr Cash/Bank Account, Cr Employee Custody Account
        /// </summary>
        Task<bool> ReturnCustodyAsync(Guid employeeId, decimal amount, Guid? destinationAccountId, string? description);

        /// <summary>
        /// Replenish custody (add more money to employee's advance).
        /// Creates: Dr Employee Custody Account, Cr Cash/Bank Account
        /// </summary>
        Task<bool> ReplenishCustodyAsync(Guid employeeId, decimal amount, Guid? sourceAccountId, string? description);

        /// <summary>
        /// Get employee custody balance from accounting system
        /// </summary>
        Task<decimal> GetEmployeeCustodyBalanceAsync(Guid employeeId);

        /// <summary>
        /// Get or create employee custody account in Chart of Accounts
        /// </summary>
        Task<Guid> GetOrCreateEmployeeCustodyAccountAsync(Guid employeeId, string employeeName);
    }
}

