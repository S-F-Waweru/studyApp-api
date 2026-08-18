using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace StudyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentAndVectorChunk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<string>(type: "text", nullable: false),
                    Filename = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    StoragePath = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NoteScribbleLinks",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScribbleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteScribbleLinks", x => new { x.NoteId, x.ScribbleId });
                });

            migrationBuilder.CreateTable(
                name: "Scribbles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CanvasData = table.Column<string>(type: "jsonb", nullable: false),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scribbles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VectorChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChunkText = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(768)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VectorChunks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ScopeId_ScopeType",
                table: "Documents",
                columns: new[] { "ScopeId", "ScopeType" });

            migrationBuilder.CreateIndex(
                name: "IX_Scribbles_ScopeId_ScopeType",
                table: "Scribbles",
                columns: new[] { "ScopeId", "ScopeType" });

            migrationBuilder.CreateIndex(
                name: "IX_VectorChunks_ScopeId_ScopeType",
                table: "VectorChunks",
                columns: new[] { "ScopeId", "ScopeType" });

            migrationBuilder.CreateIndex(
                name: "IX_VectorChunks_SourceId",
                table: "VectorChunks",
                column: "SourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "NoteScribbleLinks");

            migrationBuilder.DropTable(
                name: "Scribbles");

            migrationBuilder.DropTable(
                name: "VectorChunks");
        }
    }
}
