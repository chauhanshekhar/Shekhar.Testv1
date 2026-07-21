using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategyPerformanceAndTradeContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EntryHour",
                table: "TradeJournal",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsExpiryDay",
                table: "TradeJournal",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MarketState",
                table: "TradeJournal",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SelectedStrategy",
                table: "TradeJournal",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsExpiryDay",
                table: "Signals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MarketState",
                table: "Signals",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SelectedStrategy",
                table: "Signals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "StrategyPerformances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Strategy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MarketState = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Expiry = table.Column<bool>(type: "bit", nullable: false),
                    Hour = table.Column<int>(type: "int", nullable: false),
                    WinRate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    ProfitFactor = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Expectancy = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AverageWin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AverageLoss = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetPnL = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxDrawdown = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Trades = table.Column<int>(type: "int", nullable: false),
                    Wins = table.Column<int>(type: "int", nullable: false),
                    Losses = table.Column<int>(type: "int", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyPerformances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StrategyPerformance_Bucket",
                table: "StrategyPerformances",
                columns: new[] { "Strategy", "MarketState", "Expiry", "Hour" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StrategyPerformances");

            migrationBuilder.DropColumn(
                name: "EntryHour",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "IsExpiryDay",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "MarketState",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "SelectedStrategy",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "IsExpiryDay",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "MarketState",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "SelectedStrategy",
                table: "Signals");
        }
    }
}
