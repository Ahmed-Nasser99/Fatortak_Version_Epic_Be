using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fatortak.Migrations
{
    /// <inheritdoc />
    public partial class adddefaultschema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.RenameTable(
                name: "Tenants",
                newName: "Tenants",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "Items",
                newName: "Items",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "Invoices",
                newName: "Invoices",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "InvoiceItems",
                newName: "InvoiceItems",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "Customers",
                newName: "Customers",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "Companies",
                newName: "Companies",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                newName: "AspNetUserTokens",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                newName: "AspNetUsers",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                newName: "AspNetUserRoles",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                newName: "AspNetUserLogins",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                newName: "AspNetUserClaims",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                newName: "AspNetRoles",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                newName: "AspNetRoleClaims",
                newSchema: "dbo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Tenants",
                schema: "dbo",
                newName: "Tenants");

            migrationBuilder.RenameTable(
                name: "Items",
                schema: "dbo",
                newName: "Items");

            migrationBuilder.RenameTable(
                name: "Invoices",
                schema: "dbo",
                newName: "Invoices");

            migrationBuilder.RenameTable(
                name: "InvoiceItems",
                schema: "dbo",
                newName: "InvoiceItems");

            migrationBuilder.RenameTable(
                name: "Customers",
                schema: "dbo",
                newName: "Customers");

            migrationBuilder.RenameTable(
                name: "Companies",
                schema: "dbo",
                newName: "Companies");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                schema: "dbo",
                newName: "AspNetUserTokens");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                schema: "dbo",
                newName: "AspNetUsers");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                schema: "dbo",
                newName: "AspNetUserRoles");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                schema: "dbo",
                newName: "AspNetUserLogins");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                schema: "dbo",
                newName: "AspNetUserClaims");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                schema: "dbo",
                newName: "AspNetRoles");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                schema: "dbo",
                newName: "AspNetRoleClaims");
        }
    }
}
