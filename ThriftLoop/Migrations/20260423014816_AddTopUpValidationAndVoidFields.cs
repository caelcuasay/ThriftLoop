using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriftLoop.Migrations
{
    /// <inheritdoc />
    public partial class AddTopUpValidationAndVoidFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AccountNumberMatched",
                table: "TopUpRequests",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessedBy",
                table: "TopUpRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "TopUpRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AccountNumberUpdatedAt",
                table: "SiteSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountNumberUpdatedBy",
                table: "SiteSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GCashAccountNumber",
                table: "SiteSettings",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountNumberMatched",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "ProcessedBy",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "TopUpRequests");

            migrationBuilder.DropColumn(
                name: "AccountNumberUpdatedAt",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "AccountNumberUpdatedBy",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "GCashAccountNumber",
                table: "SiteSettings");
        }
    }
}
