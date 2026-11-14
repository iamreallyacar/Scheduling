using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TireScheduler.Api.Modules.MasterData.Migrations
{
    /// <inheritdoc />
    public partial class AddRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoutingStages",
                schema: "master_data",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    SequenceNo = table.Column<int>(type: "integer", nullable: false),
                    ResourceTypeId = table.Column<int>(type: "integer", nullable: false),
                    SetupMinutes = table.Column<int>(type: "integer", nullable: false),
                    CycleMinutes = table.Column<int>(type: "integer", nullable: false),
                    UnitsPerCycle = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingStages", x => x.Id);
                    table.CheckConstraint("CK_RoutingStages_DurationValues", "\"SequenceNo\" >= 1 AND \"SetupMinutes\" >= 0 AND \"CycleMinutes\" > 0 AND \"UnitsPerCycle\" >= 1");
                    table.ForeignKey(
                        name: "FK_RoutingStages_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "master_data",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoutingStages_ResourceTypes_ResourceTypeId",
                        column: x => x.ResourceTypeId,
                        principalSchema: "master_data",
                        principalTable: "ResourceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoutingDependencies",
                schema: "master_data",
                columns: table => new
                {
                    PredecessorStageId = table.Column<int>(type: "integer", nullable: false),
                    SuccessorStageId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    MinLagMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingDependencies", x => new { x.PredecessorStageId, x.SuccessorStageId });
                    table.CheckConstraint("CK_RoutingDependencies_LagAndStages", "\"MinLagMinutes\" >= 0 AND \"PredecessorStageId\" <> \"SuccessorStageId\"");
                    table.ForeignKey(
                        name: "FK_RoutingDependencies_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "master_data",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoutingDependencies_RoutingStages_PredecessorStageId",
                        column: x => x.PredecessorStageId,
                        principalSchema: "master_data",
                        principalTable: "RoutingStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoutingDependencies_RoutingStages_SuccessorStageId",
                        column: x => x.SuccessorStageId,
                        principalSchema: "master_data",
                        principalTable: "RoutingStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoutingDependencies_ProductId",
                schema: "master_data",
                table: "RoutingDependencies",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutingDependencies_SuccessorStageId",
                schema: "master_data",
                table: "RoutingDependencies",
                column: "SuccessorStageId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutingStages_ProductId_SequenceNo",
                schema: "master_data",
                table: "RoutingStages",
                columns: new[] { "ProductId", "SequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoutingStages_ResourceTypeId",
                schema: "master_data",
                table: "RoutingStages",
                column: "ResourceTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoutingDependencies",
                schema: "master_data");

            migrationBuilder.DropTable(
                name: "RoutingStages",
                schema: "master_data");
        }
    }
}
