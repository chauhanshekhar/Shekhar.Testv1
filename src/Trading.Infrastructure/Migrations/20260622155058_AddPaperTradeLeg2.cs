using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperTradeLeg2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Leg2EntryPrice",
                table: "PaperTrades",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Leg2OptionSymbol",
                table: "PaperTrades",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StrategyType",
                table: "PaperTrades",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Leg2EntryPrice",
                table: "PaperTrades");

            migrationBuilder.DropColumn(
                name: "Leg2OptionSymbol",
                table: "PaperTrades");

            migrationBuilder.DropColumn(
                name: "StrategyType",
                table: "PaperTrades");
        }
    }
}
