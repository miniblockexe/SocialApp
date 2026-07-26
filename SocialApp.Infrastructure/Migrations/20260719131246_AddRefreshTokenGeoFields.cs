using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenGeoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "RefreshTokens",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "RefreshTokens");
        }
    }
}
