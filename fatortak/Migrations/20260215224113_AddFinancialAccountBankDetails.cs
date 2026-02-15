using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fatortak.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialAccountBankDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankName",
                schema: "dbo",
                table: "FinancialAccounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "dbo",
                table: "FinancialAccounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Iban",
                schema: "dbo",
                table: "FinancialAccounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Swift",
                schema: "dbo",
                table: "FinancialAccounts",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankName",
                schema: "dbo",
                table: "FinancialAccounts");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "dbo",
                table: "FinancialAccounts");

            migrationBuilder.DropColumn(
                name: "Iban",
                schema: "dbo",
                table: "FinancialAccounts");

            migrationBuilder.DropColumn(
                name: "Swift",
                schema: "dbo",
                table: "FinancialAccounts");
        }
    }
}
