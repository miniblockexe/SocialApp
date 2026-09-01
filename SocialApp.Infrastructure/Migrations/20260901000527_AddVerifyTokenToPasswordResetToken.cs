using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifyTokenToPasswordResetToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsUsed",
                table: "PasswordResetTokens",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "PasswordResetTokens",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VerifyToken",
                table: "PasswordResetTokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifyTokenExpiresAt",
                table: "PasswordResetTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_VerifyToken",
                table: "PasswordResetTokens",
                column: "VerifyToken",
                unique: true,
                filter: "\"VerifyToken\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PasswordResetTokens_VerifyToken",
                table: "PasswordResetTokens");

            migrationBuilder.DropColumn(
                name: "IsCompleted",
                table: "PasswordResetTokens");

            migrationBuilder.DropColumn(
                name: "VerifyToken",
                table: "PasswordResetTokens");

            migrationBuilder.DropColumn(
                name: "VerifyTokenExpiresAt",
                table: "PasswordResetTokens");

            migrationBuilder.AlterColumn<bool>(
                name: "IsUsed",
                table: "PasswordResetTokens",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }
    }
}
