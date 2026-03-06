using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fennec.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddServerVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "visibility",
                table: "server",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "visibility",
                table: "server");
        }
    }
}
