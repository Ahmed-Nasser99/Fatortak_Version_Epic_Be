using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fatortak.Migrations
{
    /// <inheritdoc />
    public partial class AddInstallmentsLogictoinvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                schema: "dbo",
                table: "Invoices",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Benefits",
                schema: "dbo",
                table: "Invoices",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DownPayment",
                schema: "dbo",
                table: "Invoices",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Installments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Installments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Installments_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "dbo",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Installments_InvoiceId",
                schema: "dbo",
                table: "Installments",
                column: "InvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Installments",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                schema: "dbo",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Benefits",
                schema: "dbo",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DownPayment",
                schema: "dbo",
                table: "Invoices");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
