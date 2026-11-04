using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Login_and_Registration_Backend_.NET_.Migrations
{
    /// <inheritdoc />
    public partial class ImproveDataModelV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedDate",
                table: "ProductionOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProductionOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "ProductionOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "ProductionJobs",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedDate",
                table: "ProductionJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProductionJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "ProductionJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Machines",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedDate",
                table: "Machines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Machines",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "Machines",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedDate",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "ProductionJobs");

            migrationBuilder.DropColumn(
                name: "DeletedDate",
                table: "ProductionJobs");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProductionJobs");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "ProductionJobs");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "DeletedDate",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Machines");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "Machines");
        }
    }
}
