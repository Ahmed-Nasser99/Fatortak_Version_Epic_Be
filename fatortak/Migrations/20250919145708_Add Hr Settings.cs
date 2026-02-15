using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace fatortak.Migrations
{
    /// <inheritdoc />
    public partial class AddHrSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkSettings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkStartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    WorkEndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    WeekendDays = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GracePeriodMinutes = table.Column<int>(type: "int", nullable: false),
                    BreakMinutes = table.Column<int>(type: "int", nullable: false),
                    IsRespectGeneralVacation = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GeneralVacations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DaysOfVacation = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkSettingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralVacations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneralVacations_WorkSettings_WorkSettingId",
                        column: x => x.WorkSettingId,
                        principalSchema: "dbo",
                        principalTable: "WorkSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeneralVacations_WorkSettingId",
                schema: "dbo",
                table: "GeneralVacations",
                column: "WorkSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSettings_TenantId",
                schema: "dbo",
                table: "WorkSettings",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeneralVacations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "WorkSettings",
                schema: "dbo");
        }
    }
}
