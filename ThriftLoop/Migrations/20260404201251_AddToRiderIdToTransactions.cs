using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriftLoop.Migrations
{
    /// <inheritdoc />
    public partial class AddToRiderIdToTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ToUserId",
                table: "Transactions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ToRiderId",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ToRiderId",
                table: "Transactions",
                column: "ToRiderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Riders_ToRiderId",
                table: "Transactions",
                column: "ToRiderId",
                principalTable: "Riders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Riders_ToRiderId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ToRiderId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ToRiderId",
                table: "Transactions");

            migrationBuilder.AlterColumn<int>(
                name: "ToUserId",
                table: "Transactions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
