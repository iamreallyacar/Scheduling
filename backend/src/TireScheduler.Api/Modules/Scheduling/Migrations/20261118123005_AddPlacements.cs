using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TireScheduler.Api.Modules.Scheduling.Migrations
{
    /// <inheritdoc />
    public partial class AddPlacements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduleVersions",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsLive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Placements",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VersionId = table.Column<int>(type: "integer", nullable: false),
                    LotId = table.Column<int>(type: "integer", nullable: false),
                    ResourceId = table.Column<int>(type: "integer", nullable: false),
                    PlannedStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PlannedEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Placements", x => x.Id);
                    table.CheckConstraint("CK_Placements_EndAfterStart", "\"PlannedEnd\" > \"PlannedStart\"");
                    table.ForeignKey(
                        name: "FK_Placements_ScheduleVersions_VersionId",
                        column: x => x.VersionId,
                        principalSchema: "scheduling",
                        principalTable: "ScheduleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Placements_LotId",
                schema: "scheduling",
                table: "Placements",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_Placements_ResourceId",
                schema: "scheduling",
                table: "Placements",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Placements_VersionId_LotId",
                schema: "scheduling",
                table: "Placements",
                columns: new[] { "VersionId", "LotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleVersions_IsLive",
                schema: "scheduling",
                table: "ScheduleVersions",
                column: "IsLive",
                unique: true,
                filter: "\"IsLive\"");

            // Hand-written step: R2 as an exclusion constraint, so two requests at once cannot
            // double-book a resource within a version (AC-83). Half-open ranges let placements
            // touch. btree_gist lets the integer columns take part in the GiST index.
            migrationBuilder.Sql(
                $"""
                CREATE EXTENSION IF NOT EXISTS btree_gist;

                ALTER TABLE scheduling."Placements" ADD CONSTRAINT "{SchedulingDbContext.PlacementOverlapConstraint}"
                EXCLUDE USING gist ("VersionId" WITH =, "ResourceId" WITH =, tstzrange("PlannedStart", "PlannedEnd", '[)') WITH &&);
                """);

            // Hand-written data step: the live version (A41). Written as SQL without an explicit
            // id, so the id sequence stays correct for versions added later.
            migrationBuilder.Sql(
                """
                INSERT INTO scheduling."ScheduleVersions" ("Name", "IsLive") VALUES ('Live', TRUE);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Placements",
                schema: "scheduling");

            migrationBuilder.DropTable(
                name: "ScheduleVersions",
                schema: "scheduling");
        }
    }
}
