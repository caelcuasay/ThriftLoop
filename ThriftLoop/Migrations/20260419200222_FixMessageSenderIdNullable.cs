using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriftLoop.Migrations
{
    /// <inheritdoc />
    public partial class FixMessageSenderIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_SenderId_Status",
                table: "Messages");

            migrationBuilder.AlterColumn<int>(
                name: "SenderId",
                table: "Messages",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderId_Status",
                table: "Messages",
                columns: new[] { "SenderId", "Status" },
                filter: "SenderId IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_SenderId_Status",
                table: "Messages");

            migrationBuilder.AlterColumn<int>(
                name: "SenderId",
                table: "Messages",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderId_Status",
                table: "Messages",
                columns: new[] { "SenderId", "Status" });
        }
    }
}
