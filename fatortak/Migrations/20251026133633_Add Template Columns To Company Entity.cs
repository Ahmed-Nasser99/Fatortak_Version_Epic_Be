using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fatortak.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateColumnsToCompanyEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PurchaseInvoiceTemplate",
                schema: "dbo",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseInvoiceTemplateColor",
                schema: "dbo",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SaleInvoiceTemplate",
                schema: "dbo",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SaleInvoiceTemplateColor",
                schema: "dbo",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceTemplate",
                schema: "dbo",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceTemplateColor",
                schema: "dbo",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "SaleInvoiceTemplate",
                schema: "dbo",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "SaleInvoiceTemplateColor",
                schema: "dbo",
                table: "Companies");
        }
    }
}
