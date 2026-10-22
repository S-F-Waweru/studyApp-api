using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase0ChunkMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChunkIndex",
                table: "VectorChunks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "VectorChunks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HeadingPath",
                table: "VectorChunks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "VectorChunks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "VectorChunks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Locator",
                table: "VectorChunks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PipelineVersion",
                table: "VectorChunks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceTitle",
                table: "VectorChunks",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TokenCount",
                table: "VectorChunks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChunkCount",
                table: "Scribbles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "Scribbles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractionError",
                table: "Scribbles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexStatus",
                table: "Scribbles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "IndexedAt",
                table: "Scribbles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChunkCount",
                table: "Documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "Documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Documents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractionError",
                table: "Documents",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexStatus",
                table: "Documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "IndexedAt",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PageCount",
                table: "Documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "Documents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_VectorChunks_PipelineVersion",
                table: "VectorChunks",
                column: "PipelineVersion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VectorChunks_PipelineVersion",
                table: "VectorChunks");

            migrationBuilder.DropColumn(
                name: "ChunkIndex",
                table: "VectorChunks");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "VectorChunks");

            migrationBuilder.DropColumn(
                name: "HeadingPath",
                table: "VectorChunks");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "VectorChunks");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "VectorChunks");

            migrationBuilder.DropColumn(
                name: "Locator",
                table: "VectorChunks");

            migrationBuilder.DropColumn(
                name: "PipelineVersion",
                table: "VectorChunks");

            migrationBuilder.DropColumn(
                name: "SourceTitle",
                table: "VectorChunks");

            migrationBuilder.DropColumn(
                name: "TokenCount",
                table: "VectorChunks");

            migrationBuilder.DropColumn(
                name: "ChunkCount",
                table: "Scribbles");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "Scribbles");

            migrationBuilder.DropColumn(
                name: "ExtractionError",
                table: "Scribbles");

            migrationBuilder.DropColumn(
                name: "IndexStatus",
                table: "Scribbles");

            migrationBuilder.DropColumn(
                name: "IndexedAt",
                table: "Scribbles");

            migrationBuilder.DropColumn(
                name: "ChunkCount",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ExtractionError",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IndexStatus",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IndexedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "PageCount",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "Documents");
        }
    }
}
