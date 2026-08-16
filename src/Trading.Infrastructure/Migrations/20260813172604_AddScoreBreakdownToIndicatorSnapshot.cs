using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScoreBreakdownToIndicatorSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScoreAtr",
                table: "IndicatorSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreBody",
                table: "IndicatorSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreCandle",
                table: "IndicatorSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreConfidence",
                table: "IndicatorSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScoreDirection",
                table: "IndicatorSnapshots",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreEma20",
                table: "IndicatorSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreEma50",
                table: "IndicatorSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreOi",
                table: "IndicatorSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScorePcr",
                table: "IndicatorSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScorePriceChange",
                table: "IndicatorSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreRsi",
                table: "IndicatorSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreTotal",
                table: "IndicatorSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreTrend",
                table: "IndicatorSnapshots",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScoreAtr",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "ScoreBody",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "ScoreCandle",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "ScoreConfidence",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "ScoreDirection",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "ScoreEma20",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "ScoreEma50",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "ScoreOi",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "ScorePcr",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "ScorePriceChange",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "ScoreRsi",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "ScoreTotal",
                table: "IndicatorSnapshots");

            migrationBuilder.DropColumn(
                name: "ScoreTrend",
                table: "IndicatorSnapshots");
        }
    }
}
