using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarPublicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarPublicId",
                table: "Users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverPublicId",
                table: "Users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarPublicId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CoverPublicId",
                table: "Users");
        }
    }
}