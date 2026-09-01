using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiapGames.Orders.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultiItemOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GameId",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Price",
                schema: "orders",
                table: "orders");

            migrationBuilder.CreateTable(
                name: "order_items",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_items_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "orders",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_items_GameId",
                schema: "orders",
                table: "order_items",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrderId_GameId",
                schema: "orders",
                table: "order_items",
                columns: new[] { "OrderId", "GameId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_items_UserId_GameId",
                schema: "orders",
                table: "order_items",
                columns: new[] { "UserId", "GameId" },
                unique: true,
                filter: "\"Status\" <> 'Failed'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_items",
                schema: "orders");

            migrationBuilder.AddColumn<Guid>(
                name: "GameId",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                schema: "orders",
                table: "orders",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
