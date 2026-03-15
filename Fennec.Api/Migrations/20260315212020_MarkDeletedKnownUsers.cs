using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fennec.Api.Migrations
{
    /// <inheritdoc />
    public partial class MarkDeletedKnownUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "known_user",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "known_user");
        }
    }
}
