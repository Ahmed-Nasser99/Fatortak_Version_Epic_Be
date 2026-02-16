namespace fatortak.Common.Enum
{
    /// <summary>
    /// Represents the type of source document that generated a journal entry.
    /// Used for tracking the origin of accounting entries and preventing duplicate postings.
    /// </summary>
    public enum JournalEntryReferenceType
    {
        /// <summary>
        /// Manual journal entry created by user
        /// </summary>
        Manual = 1,

        /// <summary>
        /// Entry generated from an Invoice
        /// </summary>
        Invoice = 2,

        /// <summary>
        /// Entry generated from an Expense
        /// </summary>
        Expense = 3,

        /// <summary>
        /// Entry generated from a Payment transaction
        /// </summary>
        Payment = 4,

        /// <summary>
        /// Entry generated from a Purchase Invoice
        /// </summary>
        PurchaseInvoice = 5,

        /// <summary>
        /// Reversing entry (used to cancel a previous entry)
        /// </summary>
        Reversing = 6,

        /// <summary>
        /// Entry generated from Inventory adjustment
        /// </summary>
        Inventory = 7,

        /// <summary>
        /// Entry generated from Payroll
        /// </summary>
        Payroll = 8
    }
}

