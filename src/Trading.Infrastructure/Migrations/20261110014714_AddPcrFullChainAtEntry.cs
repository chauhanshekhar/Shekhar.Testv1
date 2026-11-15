using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPcrFullChainAtEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PcrFullChainAtEntry",
                table: "TradeJournal",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PcrFullChainAtEntry",
                table: "Signals",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PcrFullChainAtEntry",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "PcrFullChainAtEntry",
                table: "Signals");
        }
    }
}
