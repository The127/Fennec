using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fennec.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveServerSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_server_slug",
                table: "server");

            migrationBuilder.DropColumn(
                name: "slug",
                table: "server");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "server",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_server_slug",
                table: "server",
                column: "slug",
                unique: true);
        }
    }
}
