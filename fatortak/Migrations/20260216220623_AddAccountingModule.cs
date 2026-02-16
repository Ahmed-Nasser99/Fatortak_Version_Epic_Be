using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fatortak.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Customers_SupplierId",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_FinancialAccounts_FinancialAccountId",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_FinancialAccounts_CounterpartyAccountId",
                schema: "dbo",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_FinancialAccounts_FinancialAccountId",
                schema: "dbo",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "FinancialAccounts",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_CounterpartyAccountId",
                schema: "dbo",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_FinancialAccountId",
                schema: "dbo",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_FinancialAccountId",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_SupplierId",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CounterpartyAccountId",
                schema: "dbo",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "FinancialAccountId",
                schema: "dbo",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "FinancialAccountId",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.CreateTable(
                name: "Accounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountType = table.Column<int>(type: "int", nullable: false),
                    ParentAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Level = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsPostable = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Accounts_Accounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalSchema: "dbo",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Accounts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    ReferenceType = table.Column<int>(type: "int", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsPosted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversingEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntries_JournalEntries_ReversingEntryId",
                        column: x => x.ReversingEntryId,
                        principalSchema: "dbo",
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntryLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntryLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntryLines_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "dbo",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntryLines_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalSchema: "dbo",
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_ParentAccountId",
                schema: "dbo",
                table: "Accounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_TenantId",
                schema: "dbo",
                table: "Accounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_TenantId_AccountCode",
                schema: "dbo",
                table: "Accounts",
                columns: new[] { "TenantId", "AccountCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_TenantId_AccountType",
                schema: "dbo",
                table: "Accounts",
                columns: new[] { "TenantId", "AccountType" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_TenantId_IsActive",
                schema: "dbo",
                table: "Accounts",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ReversingEntryId",
                schema: "dbo",
                table: "JournalEntries",
                column: "ReversingEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId",
                schema: "dbo",
                table: "JournalEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_Date",
                schema: "dbo",
                table: "JournalEntries",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_EntryNumber",
                schema: "dbo",
                table: "JournalEntries",
                columns: new[] { "TenantId", "EntryNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_IsPosted",
                schema: "dbo",
                table: "JournalEntries",
                columns: new[] { "TenantId", "IsPosted" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_ReferenceType_ReferenceId",
                schema: "dbo",
                table: "JournalEntries",
                columns: new[] { "TenantId", "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TenantId_ReferenceType_ReferenceId_IsPosted",
                schema: "dbo",
                table: "JournalEntries",
                columns: new[] { "TenantId", "ReferenceType", "ReferenceId", "IsPosted" },
                filter: "[IsPosted] = 1 AND [ReferenceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_AccountId",
                schema: "dbo",
                table: "JournalEntryLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_AccountId_JournalEntryId",
                schema: "dbo",
                table: "JournalEntryLines",
                columns: new[] { "AccountId", "JournalEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_JournalEntryId",
                schema: "dbo",
                table: "JournalEntryLines",
                column: "JournalEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalEntryLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Accounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "JournalEntries",
                schema: "dbo");

            migrationBuilder.AddColumn<Guid>(
                name: "CounterpartyAccountId",
                schema: "dbo",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialAccountId",
                schema: "dbo",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialAccountId",
                schema: "dbo",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "dbo",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FinancialAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Iban = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Swift = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialAccounts_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinancialAccounts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CounterpartyAccountId",
                schema: "dbo",
                table: "Transactions",
                column: "CounterpartyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_FinancialAccountId",
                schema: "dbo",
                table: "Transactions",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_FinancialAccountId",
                schema: "dbo",
                table: "Expenses",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_SupplierId",
                schema: "dbo",
                table: "Expenses",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_EmployeeId",
                schema: "dbo",
                table: "FinancialAccounts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_TenantId",
                schema: "dbo",
                table: "FinancialAccounts",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Customers_SupplierId",
                schema: "dbo",
                table: "Expenses",
                column: "SupplierId",
                principalSchema: "dbo",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_FinancialAccounts_FinancialAccountId",
                schema: "dbo",
                table: "Expenses",
                column: "FinancialAccountId",
                principalSchema: "dbo",
                principalTable: "FinancialAccounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_FinancialAccounts_CounterpartyAccountId",
                schema: "dbo",
                table: "Transactions",
                column: "CounterpartyAccountId",
                principalSchema: "dbo",
                principalTable: "FinancialAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_FinancialAccounts_FinancialAccountId",
                schema: "dbo",
                table: "Transactions",
                column: "FinancialAccountId",
                principalSchema: "dbo",
                principalTable: "FinancialAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
