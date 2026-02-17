using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fatortak.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseCategoryAndRefactorExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                schema: "dbo",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentAccountId",
                schema: "dbo",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseCategories_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "dbo",
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExpenseCategories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CategoryId",
                schema: "dbo",
                table: "Expenses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_PaymentAccountId",
                schema: "dbo",
                table: "Expenses",
                column: "PaymentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_AccountId",
                schema: "dbo",
                table: "ExpenseCategories",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_TenantId",
                schema: "dbo",
                table: "ExpenseCategories",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Accounts_PaymentAccountId",
                schema: "dbo",
                table: "Expenses",
                column: "PaymentAccountId",
                principalSchema: "dbo",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_ExpenseCategories_CategoryId",
                schema: "dbo",
                table: "Expenses",
                column: "CategoryId",
                principalSchema: "dbo",
                principalTable: "ExpenseCategories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Accounts_PaymentAccountId",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_ExpenseCategories_CategoryId",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "ExpenseCategories",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_CategoryId",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_PaymentAccountId",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "PaymentAccountId",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "dbo",
                table: "Expenses",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
