using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PdfTranslator.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockTypeToContentBlock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlockType",
                table: "ContentBlocks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockType",
                table: "ContentBlocks");
        }
    }
}
