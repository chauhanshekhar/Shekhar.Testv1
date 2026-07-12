using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class finalentitychanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPartial",
                table: "TradeJournal",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "SignalId",
                table: "TradeJournal",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StrategyType",
                table: "TradeJournal",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOn",
                table: "Positions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "BreakevenTrades",
                table: "DailyPnL",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPartial",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "SignalId",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "StrategyType",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "UpdatedOn",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "BreakevenTrades",
                table: "DailyPnL");
        }
    }
}
