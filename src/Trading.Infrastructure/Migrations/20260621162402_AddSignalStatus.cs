using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Signals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PENDING");

            migrationBuilder.CreateIndex(
                name: "IX_Signal_Status",
                table: "Signals",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Signal_Status",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Signals");
        }
    }
}
