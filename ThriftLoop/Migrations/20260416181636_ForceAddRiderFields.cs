using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriftLoop.Migrations
{
    /// <inheritdoc />
    public partial class ForceAddRiderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.AddColumn<string>(
            //    name: "RejectionReason",
            //    table: "Riders",
            //    type: "nvarchar(500)",
            //    maxLength: 500,
            //    nullable: true);

            //migrationBuilder.AddColumn<DateTime>(
            //    name: "RejectedAt",
            //    table: "Riders",
            //    type: "datetime2",
            //    nullable: true);

            //migrationBuilder.AddColumn<DateTime>(
            //    name: "ResubmittedAt",
            //    table: "Riders",
            //    type: "datetime2",
            //    nullable: true);

            //migrationBuilder.AddColumn<DateTime>(
            //    name: "UpdatedAt",
            //    table: "Riders",
            //    type: "datetime2",
            //    nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropColumn(name: "RejectionReason", table: "Riders");
            //migrationBuilder.DropColumn(name: "RejectedAt", table: "Riders");
            //migrationBuilder.DropColumn(name: "ResubmittedAt", table: "Riders");
            //migrationBuilder.DropColumn(name: "UpdatedAt", table: "Riders");
        }
    }
}
