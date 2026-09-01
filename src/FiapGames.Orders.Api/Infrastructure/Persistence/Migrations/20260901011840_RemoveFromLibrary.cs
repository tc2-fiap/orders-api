using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiapGames.Orders.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFromLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_order_items_UserId_GameId",
                schema: "orders",
                table: "order_items");

            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedFromLibraryAtUtc",
                schema: "orders",
                table: "order_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_items_UserId_GameId",
                schema: "orders",
                table: "order_items",
                columns: new[] { "UserId", "GameId" },
                unique: true,
                filter: "\"Status\" <> 'Failed' AND \"RemovedFromLibraryAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_order_items_UserId_GameId",
                schema: "orders",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "RemovedFromLibraryAtUtc",
                schema: "orders",
                table: "order_items");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_UserId_GameId",
                schema: "orders",
                table: "order_items",
                columns: new[] { "UserId", "GameId" },
                unique: true,
                filter: "\"Status\" <> 'Failed'");
        }
    }
}
