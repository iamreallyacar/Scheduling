using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TireScheduler.Api.Modules.MasterData.Migrations
{
    /// <inheritdoc />
    public partial class CreateMasterDataSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "master_data");

            migrationBuilder.CreateTable(
                name: "Calendars",
                schema: "master_data",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsPlantDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "master_data",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Spec = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CodeUpperCase = table.Column<string>(type: "text", nullable: true, computedColumnSql: "upper(\"Code\")", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceTypes",
                schema: "master_data",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NameUpperCase = table.Column<string>(type: "text", nullable: true, computedColumnSql: "upper(\"Name\")", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarShifts",
                schema: "master_data",
                columns: table => new
                {
                    CalendarId = table.Column<int>(type: "integer", nullable: false),
                    Weekday = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StartMinuteOfDay = table.Column<int>(type: "integer", nullable: false),
                    EndMinuteOfDay = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarShifts", x => new { x.CalendarId, x.Weekday, x.StartMinuteOfDay });
                    table.CheckConstraint("CK_CalendarShifts_WithinOneDay", "\"StartMinuteOfDay\" >= 0 AND \"StartMinuteOfDay\" < \"EndMinuteOfDay\" AND \"EndMinuteOfDay\" <= 1440");
                    table.ForeignKey(
                        name: "FK_CalendarShifts_Calendars_CalendarId",
                        column: x => x.CalendarId,
                        principalSchema: "master_data",
                        principalTable: "Calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Resources",
                schema: "master_data",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResourceTypeId = table.Column<int>(type: "integer", nullable: false),
                    CalendarId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    NameUpperCase = table.Column<string>(type: "text", nullable: true, computedColumnSql: "upper(\"Name\")", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resources_Calendars_CalendarId",
                        column: x => x.CalendarId,
                        principalSchema: "master_data",
                        principalTable: "Calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Resources_ResourceTypes_ResourceTypeId",
                        column: x => x.ResourceTypeId,
                        principalSchema: "master_data",
                        principalTable: "ResourceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Calendars_IsPlantDefault",
                schema: "master_data",
                table: "Calendars",
                column: "IsPlantDefault",
                unique: true,
                filter: "\"IsPlantDefault\"");

            migrationBuilder.CreateIndex(
                name: "UX_Products_Code",
                schema: "master_data",
                table: "Products",
                column: "CodeUpperCase",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resources_CalendarId",
                schema: "master_data",
                table: "Resources",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_ResourceTypeId",
                schema: "master_data",
                table: "Resources",
                column: "ResourceTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_Resources_Name",
                schema: "master_data",
                table: "Resources",
                column: "NameUpperCase",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ResourceTypes_Name",
                schema: "master_data",
                table: "ResourceTypes",
                column: "NameUpperCase",
                unique: true);

            // Hand-written data step: the plant default calendar that every machine references,
            // Monday to Friday in three 8-hour shifts (A3, AC-40). Written as SQL without an
            // explicit id, so the id sequence stays correct for calendars added later.
            migrationBuilder.Sql(
                """
                INSERT INTO master_data."Calendars" ("Name", "IsPlantDefault") VALUES ('Plant default', TRUE);

                INSERT INTO master_data."CalendarShifts" ("CalendarId", "Weekday", "StartMinuteOfDay", "EndMinuteOfDay")
                SELECT calendar."Id", weekday.name, shift.start_minute, shift.end_minute
                FROM master_data."Calendars" AS calendar
                CROSS JOIN (VALUES ('Monday'), ('Tuesday'), ('Wednesday'), ('Thursday'), ('Friday')) AS weekday(name)
                CROSS JOIN (VALUES (0, 480), (480, 960), (960, 1440)) AS shift(start_minute, end_minute)
                WHERE calendar."IsPlantDefault";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarShifts",
                schema: "master_data");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "master_data");

            migrationBuilder.DropTable(
                name: "Resources",
                schema: "master_data");

            migrationBuilder.DropTable(
                name: "Calendars",
                schema: "master_data");

            migrationBuilder.DropTable(
                name: "ResourceTypes",
                schema: "master_data");
        }
    }
}
