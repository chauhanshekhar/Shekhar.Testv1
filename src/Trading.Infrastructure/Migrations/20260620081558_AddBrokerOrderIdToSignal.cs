using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokerOrderIdToSignal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrokerOrderId",
                table: "Signals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrokerOrderId",
                table: "Signals");
        }
    }
}
