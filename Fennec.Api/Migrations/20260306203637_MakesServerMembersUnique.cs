using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fennec.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakesServerMembersUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_server_member_user_id",
                table: "server_member");

            migrationBuilder.CreateIndex(
                name: "ix_server_member_user_id_server_id",
                table: "server_member",
                columns: new[] { "user_id", "server_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_server_member_user_id_server_id",
                table: "server_member");

            migrationBuilder.CreateIndex(
                name: "ix_server_member_user_id",
                table: "server_member",
                column: "user_id");
        }
    }
}
