using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class newlyyy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppErrors",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    StackTrace = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    InnerMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Context = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "ERROR"),
                    OccurredOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppErrors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppError_OccurredOn",
                table: "AppErrors",
                column: "OccurredOn");

            migrationBuilder.CreateIndex(
                name: "IX_AppError_Source_OccurredOn",
                table: "AppErrors",
                columns: new[] { "Source", "OccurredOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppErrors");
        }
    }
}
