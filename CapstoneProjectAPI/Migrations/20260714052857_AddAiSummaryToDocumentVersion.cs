using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapstoneProjectAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAiSummaryToDocumentVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiSummary",
                table: "DocumentVersions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiSummary",
                table: "DocumentVersions");
        }
    }
}
