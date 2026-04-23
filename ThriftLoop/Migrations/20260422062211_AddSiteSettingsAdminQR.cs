using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriftLoop.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteSettingsAdminQR : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GCashQRCodePath = table.Column<string>(type: "varchar(512)", unicode: false, maxLength: 512, nullable: true),
                    QRCodeUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QRCodeUpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TopUpRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsAutoApproved = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OcrConfidence = table.Column<float>(type: "real", nullable: true),
                    ScreenshotPath = table.Column<string>(type: "varchar(512)", unicode: false, maxLength: 512, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopUpRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopUpRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TopUpRequests_Status_CreatedAt",
                table: "TopUpRequests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TopUpRequests_UserId",
                table: "TopUpRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_TopUpRequests_ReferenceNumber",
                table: "TopUpRequests",
                column: "ReferenceNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteSettings");

            migrationBuilder.DropTable(
                name: "TopUpRequests");
        }
    }
}
