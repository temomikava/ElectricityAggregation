using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Electricity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consumption_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    building_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    month = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_consumption = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    record_count = table.Column<int>(type: "integer", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_file = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consumption_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processing_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    month = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    records_processed = table.Column<int>(type: "integer", nullable: false),
                    records_filtered = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processing_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_consumption_month",
                table: "consumption_records",
                column: "month");

            migrationBuilder.CreateIndex(
                name: "idx_region_month",
                table: "consumption_records",
                columns: new[] { "region", "month" });

            migrationBuilder.CreateIndex(
                name: "idx_logs_started",
                table: "processing_logs",
                column: "started_at",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consumption_records");

            migrationBuilder.DropTable(
                name: "processing_logs");
        }
    }
}
