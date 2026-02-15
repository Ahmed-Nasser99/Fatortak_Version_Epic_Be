namespace fatortak.Common.Enum
{
    public enum FinancialAccountType
    {
        Bank,
        Cash,
        Custody, // Employee Wallet
        Supplier // Optional: for tracking supplier balances directly if needed, but likely handled via Expenses
    }
}
