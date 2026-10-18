using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RutaProducto.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentTextExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "ctx_attachments",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ExtractionStatus",
                table: "ctx_attachments",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotApplicable")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractedText",
                table: "ctx_attachments");

            migrationBuilder.DropColumn(
                name: "ExtractionStatus",
                table: "ctx_attachments");
        }
    }
}
