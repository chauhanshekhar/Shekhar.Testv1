using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStopLossOrderTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSlUpdateTime",
                table: "Signals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StopLossOrderId",
                table: "Signals",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StopLossStatus",
                table: "Signals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "StopLossTriggerPrice",
                table: "Signals",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSlUpdateTime",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "StopLossOrderId",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "StopLossStatus",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "StopLossTriggerPrice",
                table: "Signals");
        }
    }
}
