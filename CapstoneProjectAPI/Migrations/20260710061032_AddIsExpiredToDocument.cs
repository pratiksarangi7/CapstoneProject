using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapstoneProjectAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddIsExpiredToDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsExpired",
                table: "Documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsExpired",
                table: "Documents");
        }
    }
}
