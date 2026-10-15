using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiSettingsAndWorkspaceContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomInstructions",
                table: "Workspaces",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Workspaces",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatProvider = table.Column<string>(type: "text", nullable: false),
                    ChatBaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChatModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChatApiKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Guardrails = table.Column<string>(type: "text", nullable: true),
                    SystemPromptTemplate = table.Column<string>(type: "text", nullable: true),
                    EmbeddingProvider = table.Column<string>(type: "text", nullable: false),
                    EmbeddingBaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EmbeddingModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EmbeddingApiKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EmbeddingDimensions = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiSettings");

            migrationBuilder.DropColumn(
                name: "CustomInstructions",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Workspaces");
        }
    }
}
