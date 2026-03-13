using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Fennec.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExtractKnownUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_channel_message_user_author_id",
                table: "channel_message");

            migrationBuilder.DropForeignKey(
                name: "fk_server_invite_user_created_by_user_id",
                table: "server_invite");

            migrationBuilder.DropForeignKey(
                name: "fk_server_member_user_user_id",
                table: "server_member");

            migrationBuilder.DropForeignKey(
                name: "fk_user_joined_known_server_user_user_id",
                table: "user_joined_known_server");

            migrationBuilder.DropColumn(
                name: "is_local",
                table: "user");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "user_joined_known_server",
                newName: "known_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_user_joined_known_server_user_id_known_server_id",
                table: "user_joined_known_server",
                newName: "ix_user_joined_known_server_known_user_id_known_server_id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "server_member",
                newName: "known_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_server_member_user_id_server_id",
                table: "server_member",
                newName: "ix_server_member_known_user_id_server_id");

            migrationBuilder.RenameColumn(
                name: "created_by_user_id",
                table: "server_invite",
                newName: "created_by_known_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_server_invite_created_by_user_id",
                table: "server_invite",
                newName: "ix_server_invite_created_by_known_user_id");

            migrationBuilder.CreateTable(
                name: "known_user",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    remote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_url = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_known_user", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_known_user_remote_id_instance_url",
                table: "known_user",
                columns: new[] { "remote_id", "instance_url" },
                unique: true);

            // Migrate existing users into known_user so FK constraints don't fail.
            // Local users get instance_url = NULL and remote_id = their user id.
            migrationBuilder.Sql("""
                INSERT INTO known_user (id, remote_id, instance_url, name, created_at, updated_at)
                SELECT id, id, NULL, name, created_at, updated_at
                FROM "user"
                """);

            migrationBuilder.AddForeignKey(
                name: "fk_channel_message_known_user_author_id",
                table: "channel_message",
                column: "author_id",
                principalTable: "known_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_server_invite_known_user_created_by_known_user_id",
                table: "server_invite",
                column: "created_by_known_user_id",
                principalTable: "known_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_server_member_known_user_known_user_id",
                table: "server_member",
                column: "known_user_id",
                principalTable: "known_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_user_joined_known_server_known_user_known_user_id",
                table: "user_joined_known_server",
                column: "known_user_id",
                principalTable: "known_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_channel_message_known_user_author_id",
                table: "channel_message");

            migrationBuilder.DropForeignKey(
                name: "fk_server_invite_known_user_created_by_known_user_id",
                table: "server_invite");

            migrationBuilder.DropForeignKey(
                name: "fk_server_member_known_user_known_user_id",
                table: "server_member");

            migrationBuilder.DropForeignKey(
                name: "fk_user_joined_known_server_known_user_known_user_id",
                table: "user_joined_known_server");

            migrationBuilder.DropTable(
                name: "known_user");

            migrationBuilder.RenameColumn(
                name: "known_user_id",
                table: "user_joined_known_server",
                newName: "user_id");

            migrationBuilder.RenameIndex(
                name: "ix_user_joined_known_server_known_user_id_known_server_id",
                table: "user_joined_known_server",
                newName: "ix_user_joined_known_server_user_id_known_server_id");

            migrationBuilder.RenameColumn(
                name: "known_user_id",
                table: "server_member",
                newName: "user_id");

            migrationBuilder.RenameIndex(
                name: "ix_server_member_known_user_id_server_id",
                table: "server_member",
                newName: "ix_server_member_user_id_server_id");

            migrationBuilder.RenameColumn(
                name: "created_by_known_user_id",
                table: "server_invite",
                newName: "created_by_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_server_invite_created_by_known_user_id",
                table: "server_invite",
                newName: "ix_server_invite_created_by_user_id");

            migrationBuilder.AddColumn<bool>(
                name: "is_local",
                table: "user",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "fk_channel_message_user_author_id",
                table: "channel_message",
                column: "author_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_server_invite_user_created_by_user_id",
                table: "server_invite",
                column: "created_by_user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_server_member_user_user_id",
                table: "server_member",
                column: "user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_user_joined_known_server_user_user_id",
                table: "user_joined_known_server",
                column: "user_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
