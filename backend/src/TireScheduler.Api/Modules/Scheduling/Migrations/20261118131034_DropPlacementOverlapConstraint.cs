using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TireScheduler.Api.Modules.Scheduling.Migrations
{
    /// <inheritdoc />
    public partial class DropPlacementOverlapConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-written step (DEC-43): recomputed planned ends may overlap, and FindConflicts
            // lists them instead; R2 across requests is kept by the resource row lock. btree_gist
            // stays installed, because an extension belongs to the whole database.
            migrationBuilder.Sql(
                """
                ALTER TABLE scheduling."Placements" DROP CONSTRAINT "EX_Placements_NoOverlap";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE scheduling."Placements" ADD CONSTRAINT "EX_Placements_NoOverlap"
                EXCLUDE USING gist ("VersionId" WITH =, "ResourceId" WITH =, tstzrange("PlannedStart", "PlannedEnd", '[)') WITH &&);
                """);
        }
    }
}
