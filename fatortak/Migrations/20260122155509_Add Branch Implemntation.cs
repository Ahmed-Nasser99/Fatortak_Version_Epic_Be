using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fatortak.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchImplemntation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_AspNetUsers_UserId",
                schema: "dbo",
                table: "Invoices");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                schema: "dbo",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                schema: "dbo",
                table: "Items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "dbo",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                schema: "dbo",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                schema: "dbo",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                schema: "dbo",
                table: "Expenses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableMultipleBranches",
                schema: "dbo",
                table: "Companies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Branches",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsMain = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Branches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BranchId",
                schema: "dbo",
                table: "Transactions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_BranchId",
                schema: "dbo",
                table: "Items",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BranchId",
                schema: "dbo",
                table: "Invoices",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BranchId",
                schema: "dbo",
                table: "Expenses",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_TenantId",
                schema: "dbo",
                table: "Branches",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Branches_BranchId",
                schema: "dbo",
                table: "Expenses",
                column: "BranchId",
                principalSchema: "dbo",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_AspNetUsers_UserId",
                schema: "dbo",
                table: "Invoices",
                column: "UserId",
                principalSchema: "dbo",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Branches_BranchId",
                schema: "dbo",
                table: "Invoices",
                column: "BranchId",
                principalSchema: "dbo",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Branches_BranchId",
                schema: "dbo",
                table: "Items",
                column: "BranchId",
                principalSchema: "dbo",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Branches_BranchId",
                schema: "dbo",
                table: "Transactions",
                column: "BranchId",
                principalSchema: "dbo",
                principalTable: "Branches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Branches_BranchId",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_AspNetUsers_UserId",
                schema: "dbo",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Branches_BranchId",
                schema: "dbo",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Branches_BranchId",
                schema: "dbo",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Branches_BranchId",
                schema: "dbo",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "Branches",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_BranchId",
                schema: "dbo",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Items_BranchId",
                schema: "dbo",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_BranchId",
                schema: "dbo",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_BranchId",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "BranchId",
                schema: "dbo",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BranchId",
                schema: "dbo",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "BranchId",
                schema: "dbo",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BranchId",
                schema: "dbo",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "EnableMultipleBranches",
                schema: "dbo",
                table: "Companies");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "dbo",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                schema: "dbo",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_AspNetUsers_UserId",
                schema: "dbo",
                table: "Invoices",
                column: "UserId",
                principalSchema: "dbo",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
