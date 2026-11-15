using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHeroZeroShadowCandidate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HeroZeroShadowCandidates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SnapshotTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsExpiryDay = table.Column<bool>(type: "bit", nullable: false),
                    CeHeroScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PeHeroScore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScoreGap = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    StrikePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OptionType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    TradingSymbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OptionLtp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IndexLtp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Rsi = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Pcr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AtrRaw = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AtrPct = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    WouldTradeLive = table.Column<bool>(type: "bit", nullable: false),
                    LiveDecisionReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HeroZeroShadowCandidates");
        }
    }
}
