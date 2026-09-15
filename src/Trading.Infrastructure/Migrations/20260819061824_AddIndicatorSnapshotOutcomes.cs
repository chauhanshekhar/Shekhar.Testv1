using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndicatorSnapshotOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IndicatorSnapshotOutcomes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IndicatorSnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TargetPoints = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StopLossPoints = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Target20Hit5Min = table.Column<bool>(type: "bit", nullable: true),
                    SL20Hit5Min = table.Column<bool>(type: "bit", nullable: true),
                    FirstHitResult5Min = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    Target20Hit10Min = table.Column<bool>(type: "bit", nullable: true),
                    SL20Hit10Min = table.Column<bool>(type: "bit", nullable: true),
                    FirstHitResult10Min = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    Target20Hit15Min = table.Column<bool>(type: "bit", nullable: true),
                    SL20Hit15Min = table.Column<bool>(type: "bit", nullable: true),
                    FirstHitResult15Min = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    MaxFavorableMove5 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxAdverseMove5 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxFavorableMove10 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxAdverseMove10 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxFavorableMove15 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxAdverseMove15 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CompletedWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    Anomaly = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CalculatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndicatorSnapshotOutcomes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndicatorSnapshotOutcome_CompletedWindowMinutes",
                table: "IndicatorSnapshotOutcomes",
                column: "CompletedWindowMinutes");

            migrationBuilder.CreateIndex(
                name: "IX_IndicatorSnapshotOutcome_Symbol_Direction",
                table: "IndicatorSnapshotOutcomes",
                columns: new[] { "Symbol", "Direction" });

            migrationBuilder.CreateIndex(
                name: "UX_IndicatorSnapshotOutcome_IndicatorSnapshotId",
                table: "IndicatorSnapshotOutcomes",
                column: "IndicatorSnapshotId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IndicatorSnapshotOutcomes");
        }
    }
}
