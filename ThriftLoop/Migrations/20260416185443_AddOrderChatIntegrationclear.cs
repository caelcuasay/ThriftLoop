using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriftLoop.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderChatIntegrationclear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChatConversationId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MessageType",
                table: "Messages",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "Messages",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferencedItemId",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferencedOrderId",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContextItemId",
                table: "Conversations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderId",
                table: "Conversations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_MessageType",
                table: "Messages",
                column: "MessageType");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReferencedItemId",
                table: "Messages",
                column: "ReferencedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReferencedOrderId",
                table: "Messages",
                column: "ReferencedOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ContextItemId",
                table: "Conversations",
                column: "ContextItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_OrderId",
                table: "Conversations",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Items_ContextItemId",
                table: "Conversations",
                column: "ContextItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Orders_OrderId",
                table: "Conversations",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Items_ReferencedItemId",
                table: "Messages",
                column: "ReferencedItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Orders_ReferencedOrderId",
                table: "Messages",
                column: "ReferencedOrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Items_ContextItemId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Orders_OrderId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Items_ReferencedItemId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Orders_ReferencedOrderId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_MessageType",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ReferencedItemId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ReferencedOrderId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ContextItemId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_OrderId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ChatConversationId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReferencedItemId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReferencedOrderId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ContextItemId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Conversations");
        }
    }
}
