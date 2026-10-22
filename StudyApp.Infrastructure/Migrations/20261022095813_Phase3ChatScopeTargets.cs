using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase3ChatScopeTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Workspaces",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "ChatSessions",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "ChatSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ChatSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChatSessionScopes",
                columns: table => new
                {
                    ChatSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "text", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncludeLinked = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeAncestors = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSessionScopes", x => new { x.ChatSessionId, x.TargetType, x.TargetId });
                    table.ForeignKey(
                        name: "FK_ChatSessionScopes_ChatSessions_ChatSessionId",
                        column: x => x.ChatSessionId,
                        principalTable: "ChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessionScopes_TargetType_TargetId",
                table: "ChatSessionScopes",
                columns: new[] { "TargetType", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatSessionScopes");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ChatSessions");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "ChatSessions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);
        }
    }
}
