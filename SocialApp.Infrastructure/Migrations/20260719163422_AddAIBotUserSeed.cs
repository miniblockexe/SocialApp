using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAIBotUserSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AvatarPublicId", "AvatarUrl", "BannedReason", "Bio", "CoverPhotoUrl", "CoverPublicId", "CreatedAt", "DeletedAt", "Email", "FullName", "IsActive", "LastSeen", "PasswordHash", "UpdatedAt", "Username" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), null, null, null, null, null, null, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "ai@socialapp.com", "SocialApp AI", true, null, "$2b$12$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ai_assistant" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"));
        }
    }
}
