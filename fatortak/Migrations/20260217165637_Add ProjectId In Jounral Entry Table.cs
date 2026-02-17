using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fatortak.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectIdInJounralEntryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Accounts_AccountId",
                schema: "dbo",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_AccountId",
                schema: "dbo",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AccountId",
                schema: "dbo",
                table: "Projects");

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                schema: "dbo",
                table: "JournalEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ProjectId",
                schema: "dbo",
                table: "JournalEntries",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_Projects_ProjectId",
                schema: "dbo",
                table: "JournalEntries",
                column: "ProjectId",
                principalSchema: "dbo",
                principalTable: "Projects",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_Projects_ProjectId",
                schema: "dbo",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_ProjectId",
                schema: "dbo",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "dbo",
                table: "JournalEntries");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                schema: "dbo",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_AccountId",
                schema: "dbo",
                table: "Projects",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Accounts_AccountId",
                schema: "dbo",
                table: "Projects",
                column: "AccountId",
                principalSchema: "dbo",
                principalTable: "Accounts",
                principalColumn: "Id");
        }
    }
}
