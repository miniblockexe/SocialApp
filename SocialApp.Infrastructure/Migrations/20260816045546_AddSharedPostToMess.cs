using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedPostToMess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Posts_SharedPostId",
                table: "Messages");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Posts_SharedPostId",
                table: "Messages",
                column: "SharedPostId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Posts_SharedPostId",
                table: "Messages");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Posts_SharedPostId",
                table: "Messages",
                column: "SharedPostId",
                principalTable: "Posts",
                principalColumn: "Id");
        }
    }
}
