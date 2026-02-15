using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fatortak.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RemindersCreated",
                schema: "dbo",
                table: "Subscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RemindersCreated",
                schema: "dbo",
                table: "Invoices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEngagementDate",
                schema: "dbo",
                table: "Customers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NotificationType = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId",
                schema: "dbo",
                table: "Notifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                schema: "dbo",
                table: "Notifications",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "RemindersCreated",
                schema: "dbo",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "RemindersCreated",
                schema: "dbo",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "LastEngagementDate",
                schema: "dbo",
                table: "Customers");
        }
    }
}
