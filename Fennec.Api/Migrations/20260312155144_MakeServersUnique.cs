using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fennec.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeServersUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_server_name",
                table: "server");

            migrationBuilder.CreateIndex(
                name: "ix_server_name",
                table: "server",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_server_name",
                table: "server");

            migrationBuilder.CreateIndex(
                name: "ix_server_name",
                table: "server",
                column: "name");
        }
    }
}
