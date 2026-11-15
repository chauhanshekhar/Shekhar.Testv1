using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionMinuteHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OptionMinuteHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MinuteTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TradingSymbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StrikePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OptionType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Ltp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Delta = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    ImpliedVolatility = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OpenInterest = table.Column<long>(type: "bigint", nullable: false),
                    ChangeInOi = table.Column<long>(type: "bigint", nullable: false),
                    UnderlyingLtp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionMinuteHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OptionMinuteHistory_MinuteTime",
                table: "OptionMinuteHistory",
                column: "MinuteTime");

            migrationBuilder.CreateIndex(
                name: "UX_OptionMinuteHistory_Symbol_Minute_Leg",
                table: "OptionMinuteHistory",
                columns: new[] { "Symbol", "MinuteTime", "TradingSymbol" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OptionMinuteHistory");
        }
    }
}
