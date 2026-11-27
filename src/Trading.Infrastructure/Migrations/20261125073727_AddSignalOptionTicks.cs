using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalOptionTicks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SignalOptionTicks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SignalId = table.Column<long>(type: "bigint", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OptionSymbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TickTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OptionLtp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UnderlyingLtp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OptionSL = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProtectedSL = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalOptionTicks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignalOptionTicks_SignalId_TickTime",
                table: "SignalOptionTicks",
                columns: new[] { "SignalId", "TickTime" });

            migrationBuilder.CreateIndex(
                name: "IX_SignalOptionTicks_TickTime",
                table: "SignalOptionTicks",
                column: "TickTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignalOptionTicks");
        }
    }
}
