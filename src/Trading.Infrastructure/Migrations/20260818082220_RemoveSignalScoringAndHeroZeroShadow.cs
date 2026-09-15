using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSignalScoringAndHeroZeroShadow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HeroZeroShadowCandidates");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.CreateTable(
                name: "HeroZeroShadowCandidates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AtrPct = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AtrRaw = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CeHeroScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    IndexLtp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsExpiryDay = table.Column<bool>(type: "bit", nullable: false),
                    LiveDecisionReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    OptionLtp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OptionType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Pcr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PeHeroScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Rsi = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ScoreGap = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SnapshotTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StrikePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TradingSymbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WouldTradeLive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeroZeroShadowCandidates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HeroZeroShadowCandidate_Symbol_SnapshotTime",
                table: "HeroZeroShadowCandidates",
                columns: new[] { "Symbol", "SnapshotTime" });
        }
    }
}
