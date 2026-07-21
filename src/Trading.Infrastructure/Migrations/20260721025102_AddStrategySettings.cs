using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StrategySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettingKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SettingValue = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategySettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StrategySetting_IsActive",
                table: "StrategySettings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_StrategySetting_SettingKey_Symbol",
                table: "StrategySettings",
                columns: new[] { "SettingKey", "Symbol" },
                unique: true,
                filter: "[Symbol] IS NOT NULL");

            // Seed with the values previously hardcoded in TradingSettings.MinAtrPercent so
            // behavior is unchanged the moment this migration applies — an empty table would
            // silently fall back to the caller's default (0.10m) for every symbol.
            migrationBuilder.InsertData(
                table: "StrategySettings",
                columns: new[] { "SettingKey", "Symbol", "SettingValue", "IsActive", "Description", "UpdatedOn" },
                values: new object[,]
                {
                    { "MinAtrPercent", "NIFTY", 0.04m, true, "Migrated from TradingSettings.MinAtrPercent", new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc) },
                    { "MinAtrPercent", "BANKNIFTY", 0.05m, true, "Migrated from TradingSettings.MinAtrPercent", new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc) },
                    { "MinAtrPercent", "SENSEX", 0.04m, true, "Migrated from TradingSettings.MinAtrPercent", new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc) },
                    { "MinAtrPercent", "FINNIFTY", 0.10m, true, "Migrated from TradingSettings.MinAtrPercent", new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc) },
                    { "MinAtrPercent", "MIDCAPNIFTY", 0.10m, true, "Migrated from TradingSettings.MinAtrPercent", new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc) },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StrategySettings");
        }
    }
}
