using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4RetrievalAndVision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourcesJson",
                table: "ChatMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisionApiKey",
                table: "AiSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisionBaseUrl",
                table: "AiSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisionModel",
                table: "AiSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisionProvider",
                table: "AiSettings",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourcesJson",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "VisionApiKey",
                table: "AiSettings");

            migrationBuilder.DropColumn(
                name: "VisionBaseUrl",
                table: "AiSettings");

            migrationBuilder.DropColumn(
                name: "VisionModel",
                table: "AiSettings");

            migrationBuilder.DropColumn(
                name: "VisionProvider",
                table: "AiSettings");
        }
    }
}
