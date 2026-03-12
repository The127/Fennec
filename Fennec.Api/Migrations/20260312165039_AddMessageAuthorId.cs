using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fennec.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageAuthorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "author_id",
                table: "channel_message",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_channel_message_author_id",
                table: "channel_message",
                column: "author_id");

            migrationBuilder.AddForeignKey(
                name: "fk_channel_message_user_author_id",
                table: "channel_message",
                column: "author_id",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_channel_message_user_author_id",
                table: "channel_message");

            migrationBuilder.DropIndex(
                name: "ix_channel_message_author_id",
                table: "channel_message");

            migrationBuilder.DropColumn(
                name: "author_id",
                table: "channel_message");
        }
    }
}
