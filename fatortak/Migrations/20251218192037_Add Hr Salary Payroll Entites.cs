using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fatortak.Migrations
{
    /// <inheritdoc />
    public partial class AddHrSalaryPayrollEntites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Payrolls",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpenseId = table.Column<int>(type: "int", nullable: true),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payrolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payrolls_Expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalSchema: "dbo",
                        principalTable: "Expenses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Payrolls_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payrolls_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalSchema: "dbo",
                        principalTable: "Transactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PayrollItems",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CalculatedSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DaysAttended = table.Column<int>(type: "int", nullable: false),
                    CalculationMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollItems_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollItems_Payrolls_PayrollId",
                        column: x => x.PayrollId,
                        principalSchema: "dbo",
                        principalTable: "Payrolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_EmployeeId",
                schema: "dbo",
                table: "PayrollItems",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_PayrollId",
                schema: "dbo",
                table: "PayrollItems",
                column: "PayrollId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_TenantId",
                schema: "dbo",
                table: "PayrollItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Payrolls_ExpenseId",
                schema: "dbo",
                table: "Payrolls",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_Payrolls_TenantId",
                schema: "dbo",
                table: "Payrolls",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Payrolls_TransactionId",
                schema: "dbo",
                table: "Payrolls",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Payrolls",
                schema: "dbo");
        }
    }
}
