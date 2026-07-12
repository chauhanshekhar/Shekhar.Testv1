using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class newww : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BacktestResult",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TotalTrades = table.Column<int>(type: "int", nullable: false),
                    WinningTrades = table.Column<int>(type: "int", nullable: false),
                    LosingTrades = table.Column<int>(type: "int", nullable: false),
                    WinRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetProfit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxDrawdown = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProfitFactor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AveragePnL = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestResult", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Candles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Open = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    High = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Low = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Close = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    OpenInterest = table.Column<long>(type: "bigint", nullable: false),
                    CandleTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyPnL",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalProfit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalLoss = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetProfit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTrades = table.Column<int>(type: "int", nullable: false),
                    WinTrades = table.Column<int>(type: "int", nullable: false),
                    LossTrades = table.Column<int>(type: "int", nullable: false),
                    WinRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ProfitFactor = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    MaxDrawdown = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyPnL", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EconomicEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Impact = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "HIGH"),
                    EventTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BlackoutBefore = table.Column<int>(type: "int", nullable: false),
                    BlackoutAfter = table.Column<int>(type: "int", nullable: false),
                    EnableNewsEntry = table.Column<bool>(type: "bit", nullable: false),
                    NewsEntryThresholdPct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketTicks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Open = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    High = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Low = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Close = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    TickTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketTicks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OptionChainData",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TradingSymbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StrikePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OptionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ltp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OpenInterest = table.Column<long>(type: "bigint", nullable: false),
                    ChangeInOi = table.Column<long>(type: "bigint", nullable: false),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    ImpliedVolatility = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Delta = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    Gamma = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    Theta = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    Vega = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    Rho = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    Expiry = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SnapshotId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionChainData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BrokerOrderId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Exchange = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Side = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OrderType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProductType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StatusMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TriggerPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    FilledQty = table.Column<int>(type: "int", nullable: false),
                    AvgPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OrderedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RawPostback = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaperTrades",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SignalId = table.Column<long>(type: "bigint", nullable: true),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OptionSymbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Side = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StopLoss = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Target1 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Target2 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProfitLoss = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CloseReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperTrades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Portfolios",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Capital = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UsedMargin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvailableMargin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetPnL = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Portfolios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    AvgPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PnL = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Signals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SignalType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StopLoss = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Target1 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Target2 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OptionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StrikePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OptionSymbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OptionLtp = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OptionSL = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OptionT1 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OptionT2 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Exchange = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false, defaultValue: "NSE"),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    StrategyType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "SINGLE"),
                    Leg2OptionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Leg2StrikePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Leg2Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Leg2Ltp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Leg2OrderId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RemainingQty = table.Column<int>(type: "int", nullable: false),
                    PartialPnL = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Signals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DurationDays = table.Column<int>(type: "int", nullable: false),
                    Features = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SymbolIndicators",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IndicatorName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SymbolIndicators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradeJournal",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TradeType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ProfitLoss = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TradeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeJournal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Trader"),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AutoRenew = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Candle_Symbol_CandleTime",
                table: "Candles",
                columns: new[] { "Symbol", "CandleTime" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyPnL_TradeDate",
                table: "DailyPnL",
                column: "TradeDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EconomicEvent_EventTimeUtc",
                table: "EconomicEvents",
                column: "EventTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTick_Symbol_TickTime",
                table: "MarketTicks",
                columns: new[] { "Symbol", "TickTime" });

            migrationBuilder.CreateIndex(
                name: "IX_OptionChainData_Symbol_CreatedOn",
                table: "OptionChainData",
                columns: new[] { "Symbol", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_OptionChainData_Symbol_Expiry",
                table: "OptionChainData",
                columns: new[] { "Symbol", "Expiry" });

            migrationBuilder.CreateIndex(
                name: "IX_OptionChainData_Symbol_ExpiryDate",
                table: "OptionChainData",
                columns: new[] { "Symbol", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Order_BrokerOrderId",
                table: "Orders",
                column: "BrokerOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Order_Status",
                table: "Orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaperTrade_Symbol_Status",
                table: "PaperTrades",
                columns: new[] { "Symbol", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Signal_Symbol_CreatedOn",
                table: "Signals",
                columns: new[] { "Symbol", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_SymbolIndicators_Symbol_IndicatorName",
                table: "SymbolIndicators",
                columns: new[] { "Symbol", "IndicatorName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeJournal_TradeDate",
                table: "TradeJournal",
                column: "TradeDate");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_UserId_IsActive",
                table: "UserSubscriptions",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BacktestResult");

            migrationBuilder.DropTable(
                name: "Candles");

            migrationBuilder.DropTable(
                name: "DailyPnL");

            migrationBuilder.DropTable(
                name: "EconomicEvents");

            migrationBuilder.DropTable(
                name: "MarketTicks");

            migrationBuilder.DropTable(
                name: "OptionChainData");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "PaperTrades");

            migrationBuilder.DropTable(
                name: "Portfolios");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "Signals");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropTable(
                name: "SymbolIndicators");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "TradeJournal");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "UserSubscriptions");
        }
    }
}
