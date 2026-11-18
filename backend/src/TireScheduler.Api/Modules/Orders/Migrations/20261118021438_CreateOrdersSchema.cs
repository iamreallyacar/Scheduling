using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TireScheduler.Api.Modules.Orders.Migrations
{
    /// <inheritdoc />
    public partial class CreateOrdersSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "orders");

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Priority = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    OrderNoUpperCase = table.Column<string>(type: "text", nullable: true, computedColumnSql: "upper(\"OrderNo\")", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_Quantity", "\"Quantity\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "Operations",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    SequenceNo = table.Column<int>(type: "integer", nullable: false),
                    ResourceTypeId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    SetupMinutes = table.Column<int>(type: "integer", nullable: false),
                    CycleMinutes = table.Column<int>(type: "integer", nullable: false),
                    UnitsPerCycle = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => x.Id);
                    table.CheckConstraint("CK_Operations_Values", "\"SequenceNo\" >= 1 AND \"Quantity\" >= 1 AND \"SetupMinutes\" >= 0 AND \"CycleMinutes\" > 0 AND \"UnitsPerCycle\" >= 1");
                    table.ForeignKey(
                        name: "FK_Operations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "orders",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Lots",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OperationId = table.Column<int>(type: "integer", nullable: false),
                    LotNo = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lots", x => x.Id);
                    table.CheckConstraint("CK_Lots_Values", "\"LotNo\" >= 1 AND \"Quantity\" >= 1 AND \"DurationMinutes\" > 0");
                    table.ForeignKey(
                        name: "FK_Lots_Operations_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "orders",
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperationDependencies",
                schema: "orders",
                columns: table => new
                {
                    PredecessorOperationId = table.Column<int>(type: "integer", nullable: false),
                    SuccessorOperationId = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    MinLagMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationDependencies", x => new { x.PredecessorOperationId, x.SuccessorOperationId });
                    table.CheckConstraint("CK_OperationDependencies_LagAndOperations", "\"MinLagMinutes\" >= 0 AND \"PredecessorOperationId\" <> \"SuccessorOperationId\"");
                    table.ForeignKey(
                        name: "FK_OperationDependencies_Operations_PredecessorOperationId",
                        column: x => x.PredecessorOperationId,
                        principalSchema: "orders",
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperationDependencies_Operations_SuccessorOperationId",
                        column: x => x.SuccessorOperationId,
                        principalSchema: "orders",
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperationDependencies_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "orders",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lots_OperationId_LotNo",
                schema: "orders",
                table: "Lots",
                columns: new[] { "OperationId", "LotNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationDependencies_OrderId",
                schema: "orders",
                table: "OperationDependencies",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationDependencies_SuccessorOperationId",
                schema: "orders",
                table: "OperationDependencies",
                column: "SuccessorOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_Operations_OrderId_SequenceNo",
                schema: "orders",
                table: "Operations",
                columns: new[] { "OrderId", "SequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Orders_OrderNo",
                schema: "orders",
                table: "Orders",
                column: "OrderNoUpperCase",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Lots",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "OperationDependencies",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "Operations",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "orders");
        }
    }
}
