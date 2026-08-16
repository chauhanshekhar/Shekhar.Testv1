using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockedLegToIndicatorSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BlockedChangeInOi",
                table: "IndicatorSnapshots",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BlockedDelta",
                table: "IndicatorSnapshots",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BlockedOptionLtp",
                table: "IndicatorSnapshots",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlockedOptionSymbol",
                table: "IndicatorSnapshots",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlockedOptionType",
                table: "IndicatorSnapshots",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BlockedStrikePrice",
                table: "IndicatorSnapshots",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockedChangeInOi",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "BlockedDelta",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "BlockedOptionLtp",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "BlockedOptionSymbol",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "BlockedOptionType",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "BlockedStrikePrice",
                table: "IndicatorSnapshots");
        }
    }
}
