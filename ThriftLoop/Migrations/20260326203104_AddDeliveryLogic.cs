using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriftLoop.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryLogic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveDeliveryId",
                table: "Riders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActiveDeliveryStartedAt",
                table: "Riders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    RiderId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PickedUpAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedByBuyerAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deliveries_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Deliveries_Riders_RiderId",
                        column: x => x.RiderId,
                        principalTable: "Riders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Riders_ActiveDeliveryId",
                table: "Riders",
                column: "ActiveDeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_OrderId",
                table: "Deliveries",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_RiderId",
                table: "Deliveries",
                column: "RiderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Riders_Deliveries_ActiveDeliveryId",
                table: "Riders",
                column: "ActiveDeliveryId",
                principalTable: "Deliveries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Riders_Deliveries_ActiveDeliveryId",
                table: "Riders");

            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropIndex(
                name: "IX_Riders_ActiveDeliveryId",
                table: "Riders");

            migrationBuilder.DropColumn(
                name: "ActiveDeliveryId",
                table: "Riders");

            migrationBuilder.DropColumn(
                name: "ActiveDeliveryStartedAt",
                table: "Riders");
        }
    }
}
