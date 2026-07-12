using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLifecycleTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Order_BrokerOrderId",
                table: "Orders");

            migrationBuilder.AddColumn<long>(
                name: "EntryOrderId",
                table: "TradeJournal",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExitOrderId",
                table: "TradeJournal",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExitReason",
                table: "TradeJournal",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrokerOrderStatus",
                table: "Signals",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastOrderSyncTime",
                table: "Signals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderReferenceId",
                table: "Signals",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "OrderSyncAttempts",
                table: "Signals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Positions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EntryOrderId",
                table: "Positions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenedAt",
                table: "Positions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "OptionSymbol",
                table: "Positions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PositionSource",
                table: "Positions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "BROKER_SYNC");

            migrationBuilder.AddColumn<long>(
                name: "SignalId",
                table: "Positions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Positions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "OPEN");

            migrationBuilder.AddColumn<string>(
                name: "Broker",
                table: "Orders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "GROWW");

            migrationBuilder.AddColumn<string>(
                name: "OrderReferenceId",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SignalId",
                table: "Orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Position_PositionSource",
                table: "Positions",
                column: "PositionSource");

            migrationBuilder.CreateIndex(
                name: "IX_Position_SignalId",
                table: "Positions",
                column: "SignalId");

            migrationBuilder.CreateIndex(
                name: "IX_Order_BrokerOrderId",
                table: "Orders",
                column: "BrokerOrderId",
                unique: true,
                filter: "[BrokerOrderId] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Order_OrderReferenceId",
                table: "Orders",
                column: "OrderReferenceId",
                unique: true,
                filter: "[OrderReferenceId] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Order_SignalId",
                table: "Orders",
                column: "SignalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Position_PositionSource",
                table: "Positions");

            migrationBuilder.DropIndex(
                name: "IX_Position_SignalId",
                table: "Positions");

            migrationBuilder.DropIndex(
                name: "IX_Order_BrokerOrderId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Order_OrderReferenceId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Order_SignalId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EntryOrderId",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "ExitOrderId",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "ExitReason",
                table: "TradeJournal");

            migrationBuilder.DropColumn(
                name: "BrokerOrderStatus",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "LastOrderSyncTime",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "OrderReferenceId",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "OrderSyncAttempts",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "EntryOrderId",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "OpenedAt",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "OptionSymbol",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "PositionSource",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "SignalId",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "Broker",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderReferenceId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SignalId",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Order_BrokerOrderId",
                table: "Orders",
                column: "BrokerOrderId",
                unique: true);
        }
    }
}
