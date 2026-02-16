# Custody Implementation with Chart of Accounts

## Overview

This document explains how **Custody** (employee advances) is handled using the **Chart of Accounts** (`Account` entity) instead of `FinancialAccount`.

## Key Design Decision

**`FinancialAccount` and `Account` are completely separate systems:**

- **`FinancialAccount`**: Operational accounts for tracking cash, bank accounts, and basic financial movements. Used for day-to-day operational tracking.
- **`Account` (Chart of Accounts)**: Accounting system for double-entry bookkeeping. Used for proper accounting records and financial reporting.

**Custody operations use `Account` entities, NOT `FinancialAccount`.**

## Custody Flow

### 1. Employee Custody Account Structure

- **Parent Account**: `1500 - Employee Custody` (Asset account, non-postable)
- **Child Accounts**: `1500-{EmployeeId}` - Individual employee custody accounts (postable)

Example:
- `1500` - Employee Custody (parent)
  - `1500-12345678` - Custody - Ahmed Mohamed
  - `1500-87654321` - Custody - Sara Ali

### 2. Custody Operations

#### Give Custody (Advance Payment)
**Journal Entry:**
- Dr Employee Custody Account (e.g., `1500-12345678`)
- Cr Cash/Bank Account (e.g., `1000` or `1100`)

**API Endpoint:** `POST /api/accounting/custody/give`

#### Use Custody for Expense
**Journal Entry:**
- Dr Expense Account (e.g., `5000` or category-specific)
- Cr Employee Custody Account (e.g., `1500-12345678`)

**API Endpoint:** `POST /api/accounting/custody/use-for-expense`

#### Return Custody (Unused Advance)
**Journal Entry:**
- Dr Cash/Bank Account (e.g., `1000`)
- Cr Employee Custody Account (e.g., `1500-12345678`)

**API Endpoint:** `POST /api/accounting/custody/return`

#### Replenish Custody (Add More Money)
**Journal Entry:**
- Dr Employee Custody Account (e.g., `1500-12345678`)
- Cr Cash/Bank Account (e.g., `1000`)

**API Endpoint:** `POST /api/accounting/custody/replenish`

### 3. Get Custody Balance

**API Endpoint:** `GET /api/accounting/custody/balance/{employeeId}`

Returns the current custody balance for an employee, calculated from journal entries:
- **Balance Formula for Asset Accounts**: `Balance = Debit - Credit`
- Positive balance = employee owes company (has advance)
- Negative balance = company owes employee (overpaid)

## Implementation Details

### CustodyService

The `CustodyService` handles all custody operations:

- **`GetOrCreateEmployeeCustodyAccountAsync`**: Automatically creates employee-specific custody account if it doesn't exist
- **`GiveCustodyAsync`**: Creates journal entry for giving advance
- **`UseCustodyForExpenseAsync`**: Creates journal entry when expense is paid from custody
- **`ReturnCustodyAsync`**: Creates journal entry for returning unused advance
- **`ReplenishCustodyAsync`**: Creates journal entry for adding more money to custody
- **`GetEmployeeCustodyBalanceAsync`**: Calculates balance from journal entries

### Account Seeding

The `AccountSeeder` automatically creates the parent account `1500 - Employee Custody` when seeding the Chart of Accounts for a tenant.

## Usage Example

```csharp
// Give custody to employee
var giveCustodyDto = new GiveCustodyDto
{
    EmployeeId = employeeId,
    Amount = 5000,
    SourceAccountId = cashAccountId, // From Chart of Accounts
    Description = "Monthly advance"
};
await _custodyService.GiveCustodyAsync(...);

// Use custody for expense
var useCustodyDto = new UseCustodyForExpenseDto
{
    ExpenseId = expenseId,
    EmployeeId = employeeId
};
await _custodyService.UseCustodyForExpenseAsync(...);

// Get balance
var balance = await _custodyService.GetEmployeeCustodyBalanceAsync(employeeId);
```

## Important Notes

1. **All custody operations create journal entries** - ensuring proper double-entry bookkeeping
2. **Custody accounts are Asset accounts** - they represent money owed by employees
3. **Automatic account creation** - employee custody accounts are created on-demand
4. **Multi-tenant support** - all operations respect tenant isolation
5. **Transactional integrity** - all operations use database transactions

## FinancialAccount vs Account

| Feature | FinancialAccount | Account (Chart of Accounts) |
|---------|------------------|---------------------------|
| Purpose | Operational tracking | Double-entry accounting |
| Balance Calculation | Direct field update | Calculated from journal entries |
| Custody Support | ❌ Not used | ✅ Used |
| Accounting Integration | ❌ Separate | ✅ Integrated |
| Financial Reports | ❌ Not included | ✅ Included |

## Migration Notes

- No database migration needed for custody implementation
- The `1500 - Employee Custody` parent account is created during account seeding
- Employee-specific accounts are created automatically when needed

