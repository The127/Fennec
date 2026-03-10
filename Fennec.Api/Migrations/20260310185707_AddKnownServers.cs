using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Fennec.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddKnownServers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "known_server",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    remote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_url = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_known_server", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_joined_known_server",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    known_server_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_joined_known_server", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_joined_known_server_known_server_known_server_id",
                        column: x => x.known_server_id,
                        principalTable: "known_server",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_joined_known_server_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_known_server_remote_id_instance_url",
                table: "known_server",
                columns: new[] { "remote_id", "instance_url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_joined_known_server_known_server_id",
                table: "user_joined_known_server",
                column: "known_server_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_joined_known_server_user_id_known_server_id",
                table: "user_joined_known_server",
                columns: new[] { "user_id", "known_server_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_joined_known_server");

            migrationBuilder.DropTable(
                name: "known_server");
        }
    }
}
