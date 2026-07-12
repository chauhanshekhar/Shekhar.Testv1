using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class tradegenralentitychnages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ChangeInOiAtEntry",
                table: "TradeJournal",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeltaAtEntry",
                table: "TradeJournal",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PcrAtEntry",
                table: "TradeJournal",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RsiAtEntry",
                table: "TradeJournal",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SignalConfidence",
                table: "TradeJournal",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ChangeInOiAtEntry",
                table: "Signals",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeltaAtEntry",
                table: "Signals",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PcrAtEntry",
                table: "Signals",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RsiAtEntry",
                table: "Signals",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChangeInOiAtEntry",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "DeltaAtEntry",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "PcrAtEntry",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "RsiAtEntry",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "SignalConfidence",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "ChangeInOiAtEntry",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "DeltaAtEntry",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "PcrAtEntry",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "RsiAtEntry",
                table: "Signals");
        }
    }
}
