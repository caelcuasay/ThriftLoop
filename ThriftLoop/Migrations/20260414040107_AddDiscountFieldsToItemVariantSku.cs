using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriftLoop.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountFieldsToItemVariantSku : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "ItemVariantSkus",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPrice",
                table: "ItemVariantSkus",
                type: "decimal(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "ItemVariantSkus");

            migrationBuilder.DropColumn(
                name: "OriginalPrice",
                table: "ItemVariantSkus");
        }
    }
}
