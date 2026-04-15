using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriftLoop.Migrations
{
    /// <inheritdoc />
    public partial class AddFulfillmentDistinctions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ChatInitialized",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ChatSessionId",
                table: "Orders",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FulfillmentMethod",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AllowDelivery",
                table: "Items",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowHalfway",
                table: "Items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowPickup",
                table: "Items",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatInitialized",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ChatSessionId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AllowDelivery",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "AllowHalfway",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "AllowPickup",
                table: "Items");
        }
    }
}
