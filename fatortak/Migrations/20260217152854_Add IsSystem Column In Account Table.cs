using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fatortak.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSystemColumnInAccountTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                schema: "dbo",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                schema: "dbo",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                schema: "dbo",
                table: "Accounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_AccountId",
                schema: "dbo",
                table: "Projects",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_AccountId",
                schema: "dbo",
                table: "Customers",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Accounts_AccountId",
                schema: "dbo",
                table: "Customers",
                column: "AccountId",
                principalSchema: "dbo",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Accounts_AccountId",
                schema: "dbo",
                table: "Projects",
                column: "AccountId",
                principalSchema: "dbo",
                principalTable: "Accounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Accounts_AccountId",
                schema: "dbo",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Accounts_AccountId",
                schema: "dbo",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_AccountId",
                schema: "dbo",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Customers_AccountId",
                schema: "dbo",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "AccountId",
                schema: "dbo",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AccountId",
                schema: "dbo",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                schema: "dbo",
                table: "Accounts");
        }
    }
}
