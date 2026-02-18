using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fatortak.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectLineEntityTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Branches_Tenants_TenantId",
                schema: "dbo",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "EndDate",
                schema: "dbo",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "StartDate",
                schema: "dbo",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TotalBudget",
                schema: "dbo",
                table: "Projects");

            migrationBuilder.AddColumn<decimal>(
                name: "ContractValue",
                schema: "dbo",
                table: "Projects",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "dbo",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTerms",
                schema: "dbo",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectLines_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "dbo",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLines_ProjectId",
                schema: "dbo",
                table: "ProjectLines",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Tenants_TenantId",
                schema: "dbo",
                table: "Branches",
                column: "TenantId",
                principalSchema: "dbo",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Branches_Tenants_TenantId",
                schema: "dbo",
                table: "Branches");

            migrationBuilder.DropTable(
                name: "ProjectLines",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "ContractValue",
                schema: "dbo",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "dbo",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PaymentTerms",
                schema: "dbo",
                table: "Projects");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                schema: "dbo",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                schema: "dbo",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalBudget",
                schema: "dbo",
                table: "Projects",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Tenants_TenantId",
                schema: "dbo",
                table: "Branches",
                column: "TenantId",
                principalSchema: "dbo",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
