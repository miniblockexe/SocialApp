using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixSearchCollation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Users"" 
                    ALTER COLUMN ""Username"" TYPE text COLLATE pg_catalog.""default"",
                    ALTER COLUMN ""FullName"" TYPE text COLLATE pg_catalog.""default"";

                ALTER TABLE ""Posts"" 
                    ALTER COLUMN ""Content"" TYPE text COLLATE pg_catalog.""default"";
    ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Users"" 
                    ALTER COLUMN ""Username"" TYPE text,
                    ALTER COLUMN ""FullName"" TYPE text;

                ALTER TABLE ""Posts"" 
                    ALTER COLUMN ""Content"" TYPE text;
            ");
        }
    }
}
