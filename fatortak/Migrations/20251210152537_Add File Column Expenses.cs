using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fatortak.Migrations
{
    /// <inheritdoc />
    public partial class AddFileColumnExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                schema: "dbo",
                table: "Expenses",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FilePath",
                schema: "dbo",
                table: "Expenses");
        }
    }
}
