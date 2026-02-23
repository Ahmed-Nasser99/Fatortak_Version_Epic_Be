namespace fatortak.Common.Enum
{
    /// <summary>
    /// Represents the type of account in the Chart of Accounts.
    /// Used for categorizing accounts and determining balance calculation rules.
    /// </summary>
    public enum AccountType
    {
        /// <summary>
        /// Assets: Resources owned by the business (e.g., Cash, Accounts Receivable, Inventory)
        /// Balance = Debit - Credit
        /// </summary>
        Asset = 1,

        /// <summary>
        /// Liabilities: Obligations owed by the business (e.g., Accounts Payable, Loans)
        /// Balance = Credit - Debit
        /// </summary>
        Liability = 2,

        /// <summary>
        /// Equity: Owner's interest in the business (e.g., Capital, Retained Earnings)
        /// Balance = Credit - Debit
        /// </summary>
        Equity = 3,

        /// <summary>
        /// Revenue: Income generated from business operations (e.g., Sales, Service Revenue)
        /// Balance = Credit - Debit
        /// </summary>
        Revenue = 4,

        /// <summary>
        /// Expense: Costs incurred in business operations (e.g., Rent, Salaries, Utilities)
        /// Balance = Debit - Credit
        /// </summary>
        Expense = 5,

        /// <summary>
        /// Cash: Liquid cash on hand
        /// Balance = Debit - Credit
        /// </summary>
        Cash = 6,

        /// <summary>
        /// Bank: Funds in bank accounts
        /// Balance = Debit - Credit
        /// </summary>
        Bank = 7,

        /// <summary>
        /// Other: Miscellaneous accounts
        /// </summary>
        Other = 8
    }
}

