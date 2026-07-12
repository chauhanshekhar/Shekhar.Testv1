using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTradingInstruments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradingInstruments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Exchange = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false, defaultValue: "NSE"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Expiry = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingInstruments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradingInstrument_Symbol",
                table: "TradingInstruments",
                column: "Symbol",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradingInstruments");
        }
    }
}
