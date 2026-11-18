using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TireScheduler.Api.Modules.Scheduling.Migrations
{
    /// <inheritdoc />
    public partial class CreateSchedulingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "scheduling");

            migrationBuilder.CreateTable(
                name: "Unavailabilities",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResourceId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unavailabilities", x => x.Id);
                    table.CheckConstraint("CK_Unavailabilities_EndAfterStart", "\"EndsAt\" IS NULL OR \"EndsAt\" > \"StartsAt\"");
                    table.CheckConstraint("CK_Unavailabilities_OpenEndOnlyForBreakdown", "\"EndsAt\" IS NOT NULL OR \"Kind\" = 'Breakdown'");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Unavailabilities_ResourceId_StartsAt",
                schema: "scheduling",
                table: "Unavailabilities",
                columns: new[] { "ResourceId", "StartsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Unavailabilities",
                schema: "scheduling");
        }
    }
}
