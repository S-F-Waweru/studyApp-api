using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase7OwnershipExpand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Scribbles_ScopeId_ScopeType",
                table: "Scribbles");

            migrationBuilder.DropIndex(
                name: "IX_Notes_ScopeId_ScopeType",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ScopeId_ScopeType",
                table: "Documents");

            migrationBuilder.RenameColumn(
                name: "CreatedByUserId",
                table: "ChatSessions",
                newName: "OwnerUserId");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Scribbles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Notes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_OwnerUserId",
                table: "Workspaces",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Scribbles_OwnerUserId_ScopeId_ScopeType",
                table: "Scribbles",
                columns: new[] { "OwnerUserId", "ScopeId", "ScopeType" });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_OwnerUserId_ScopeId_ScopeType",
                table: "Notes",
                columns: new[] { "OwnerUserId", "ScopeId", "ScopeType" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OwnerUserId_ScopeId_ScopeType",
                table: "Documents",
                columns: new[] { "OwnerUserId", "ScopeId", "ScopeType" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_OwnerUserId",
                table: "ChatSessions",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatSessions_AspNetUsers_OwnerUserId",
                table: "ChatSessions",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_AspNetUsers_OwnerUserId",
                table: "Documents",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_AspNetUsers_OwnerUserId",
                table: "Notes",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Scribbles_AspNetUsers_OwnerUserId",
                table: "Scribbles",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_AspNetUsers_OwnerUserId",
                table: "Workspaces",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Phase 7.3 backfill — adopt every pre-auth row into the legacy owner.
            // Workspaces must run first: the four statements below read OwnerUserId from them.
            // No ELSE in the CASE clauses: a row whose scope no longer exists stays NULL on purpose,
            // so the post-migration check reports it instead of silently adopting an orphan.
            migrationBuilder.Sql("""
                UPDATE "Workspaces"
                SET "OwnerUserId" = 'b3fab60c-8d65-445b-a605-6166c0d5410d'::uuid
                WHERE "OwnerUserId" IS NULL;

                UPDATE "Notes" t SET "OwnerUserId" = CASE
                    WHEN lower(t."ScopeType") = 'workspace' THEN
                        (SELECT w."OwnerUserId" FROM "Workspaces" w WHERE w."Id" = t."ScopeId")
                    WHEN lower(t."ScopeType") = 'folder' THEN
                        (SELECT w."OwnerUserId" FROM "Folders" f
                         JOIN "Workspaces" w ON w."Id" = f."WorkspaceId"
                         WHERE f."Id" = t."ScopeId")
                END
                WHERE t."OwnerUserId" IS NULL;

                UPDATE "Documents" t SET "OwnerUserId" = CASE
                    WHEN lower(t."ScopeType") = 'workspace' THEN
                        (SELECT w."OwnerUserId" FROM "Workspaces" w WHERE w."Id" = t."ScopeId")
                    WHEN lower(t."ScopeType") = 'folder' THEN
                        (SELECT w."OwnerUserId" FROM "Folders" f
                         JOIN "Workspaces" w ON w."Id" = f."WorkspaceId"
                         WHERE f."Id" = t."ScopeId")
                END
                WHERE t."OwnerUserId" IS NULL;

                UPDATE "Scribbles" t SET "OwnerUserId" = CASE
                    WHEN lower(t."ScopeType") = 'workspace' THEN
                        (SELECT w."OwnerUserId" FROM "Workspaces" w WHERE w."Id" = t."ScopeId")
                    WHEN lower(t."ScopeType") = 'folder' THEN
                        (SELECT w."OwnerUserId" FROM "Folders" f
                         JOIN "Workspaces" w ON w."Id" = f."WorkspaceId"
                         WHERE f."Id" = t."ScopeId")
                END
                WHERE t."OwnerUserId" IS NULL;

                UPDATE "ChatSessions" t SET "OwnerUserId" = CASE
                    WHEN lower(t."ScopeType") = 'workspace' THEN
                        (SELECT w."OwnerUserId" FROM "Workspaces" w WHERE w."Id" = t."ScopeId")
                    WHEN lower(t."ScopeType") = 'folder' THEN
                        (SELECT w."OwnerUserId" FROM "Folders" f
                         JOIN "Workspaces" w ON w."Id" = f."WorkspaceId"
                         WHERE f."Id" = t."ScopeId")
                END
                WHERE t."OwnerUserId" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatSessions_AspNetUsers_OwnerUserId",
                table: "ChatSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_AspNetUsers_OwnerUserId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Notes_AspNetUsers_OwnerUserId",
                table: "Notes");

            migrationBuilder.DropForeignKey(
                name: "FK_Scribbles_AspNetUsers_OwnerUserId",
                table: "Scribbles");

            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_AspNetUsers_OwnerUserId",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Workspaces_OwnerUserId",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Scribbles_OwnerUserId_ScopeId_ScopeType",
                table: "Scribbles");

            migrationBuilder.DropIndex(
                name: "IX_Notes_OwnerUserId_ScopeId_ScopeType",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Documents_OwnerUserId_ScopeId_ScopeType",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_OwnerUserId",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Scribbles");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Documents");

            migrationBuilder.RenameColumn(
                name: "OwnerUserId",
                table: "ChatSessions",
                newName: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Scribbles_ScopeId_ScopeType",
                table: "Scribbles",
                columns: new[] { "ScopeId", "ScopeType" });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_ScopeId_ScopeType",
                table: "Notes",
                columns: new[] { "ScopeId", "ScopeType" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ScopeId_ScopeType",
                table: "Documents",
                columns: new[] { "ScopeId", "ScopeType" });
        }
    }
}
