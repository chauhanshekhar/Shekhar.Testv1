using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalAtrPctAtEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AtrPctAtEntry",
                table: "Signals",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtrPctAtEntry",
                table: "Signals");
        }
    }
}
