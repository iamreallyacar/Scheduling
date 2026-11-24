using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TireScheduler.Api.Modules.Progress.Migrations
{
    /// <inheritdoc />
    public partial class CreateProgressSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "progress");

            migrationBuilder.CreateTable(
                name: "LotProgress",
                schema: "progress",
                columns: table => new
                {
                    LotId = table.Column<int>(type: "integer", nullable: false),
                    ActualStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActualEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotProgress", x => x.LotId);
                    table.CheckConstraint("CK_LotProgress_EndAfterStart", "\"ActualEnd\" IS NULL OR \"ActualEnd\" > \"ActualStart\"");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotProgress",
                schema: "progress");
        }
    }
}
