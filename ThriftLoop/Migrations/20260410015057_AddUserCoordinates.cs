using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriftLoop.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Users",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Users",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Users");

        }
    }
}
