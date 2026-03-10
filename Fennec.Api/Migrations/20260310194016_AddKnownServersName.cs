using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fennec.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddKnownServersName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "known_server",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "name",
                table: "known_server");
        }
    }
}
