using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Login_and_Registration_Backend_.NET_.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeIndexesAndPerformanceImprovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add composite indexes for common query patterns
            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_Status_DueDate",
                table: "ProductionOrders",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_Status_Priority",
                table: "ProductionOrders",
                columns: new[] { "Status", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_CreatedDate_Status",
                table: "ProductionOrders",
                columns: new[] { "CreatedDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionJobs_Status_ScheduledStartTime",
                table: "ProductionJobs",
                columns: new[] { "Status", "ScheduledStartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionJobs_MachineId_Status",
                table: "ProductionJobs",
                columns: new[] { "MachineId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Machines_Status_Type",
                table: "Machines",
                columns: new[] { "Status", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Machines_IsActive_Type",
                table: "Machines",
                columns: new[] { "IsActive", "Type" });

            // Include seed data for initial setup - only if no machines exist
            // Note: This check will be handled by the DatabaseSeedingService
            // Adding basic machine setup for immediate use
            migrationBuilder.InsertData(
                table: "Machines",
                columns: new[] { "Name", "Type", "Status", "Utilization", "IsActive", "CreatedDate", "IsDeleted" },
                values: new object[,]
                {
                    { "Molding Press 1", "Molding Press", "idle", 0, true, new DateTime(2025, 6, 30, 1, 7, 22, 0, DateTimeKind.Utc), false },
                    { "Molding Press 2", "Molding Press", "idle", 0, true, new DateTime(2025, 6, 30, 1, 7, 22, 0, DateTimeKind.Utc), false },
                    { "Building Machine 1", "Building Machine", "idle", 0, true, new DateTime(2025, 6, 30, 1, 7, 22, 0, DateTimeKind.Utc), false },
                    { "Extrusion Line 1", "Extrusion Line", "idle", 0, true, new DateTime(2025, 6, 30, 1, 7, 22, 0, DateTimeKind.Utc), false },
                    { "QC Station 1", "QC Station", "idle", 0, true, new DateTime(2025, 6, 30, 1, 7, 22, 0, DateTimeKind.Utc), false }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove seed data
            migrationBuilder.DeleteData(
                table: "Machines",
                keyColumn: "Name",
                keyValues: new object[] { "Molding Press 1", "Molding Press 2", "Building Machine 1", "Extrusion Line 1", "QC Station 1" });

            // Drop composite indexes
            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_Status_DueDate",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_Status_Priority",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_CreatedDate_Status",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionJobs_Status_ScheduledStartTime",
                table: "ProductionJobs");

            migrationBuilder.DropIndex(
                name: "IX_ProductionJobs_MachineId_Status",
                table: "ProductionJobs");

            migrationBuilder.DropIndex(
                name: "IX_Machines_Status_Type",
                table: "Machines");

            migrationBuilder.DropIndex(
                name: "IX_Machines_IsActive_Type",
                table: "Machines");
        }
    }
}
