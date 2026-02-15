namespace fatortak.Common.Enum
{
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum FinancialAccountType
    {
        Bank,
        Cash,
        Custody, // Employee Wallet
        Supplier // Optional: for tracking supplier balances directly if needed, but likely handled via Expenses
    }
}
