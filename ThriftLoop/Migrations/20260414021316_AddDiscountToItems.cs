using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriftLoop.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountToItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DiscountExpiresAt",
                table: "Items",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "Items",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscountedAt",
                table: "Items",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPrice",
                table: "Items",
                type: "decimal(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountExpiresAt",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "DiscountedAt",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "OriginalPrice",
                table: "Items");
        }
    }
}
