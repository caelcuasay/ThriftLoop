using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriftLoop.Migrations
{
    /// <inheritdoc />
    public partial class AddInquiryFieldsToConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InquiryExpiresAt",
                table: "Conversations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InquiryRespondedAt",
                table: "Conversations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InquiryStatus",
                table: "Conversations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InquiryExpiresAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "InquiryRespondedAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "InquiryStatus",
                table: "Conversations");
        }
    }
}
