using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace schedule.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicProfileSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicSlug",
                table: "UserProfiles",
                type: "nvarchar(90)",
                maxLength: 90,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_PublicSlug",
                table: "UserProfiles",
                column: "PublicSlug",
                unique: true,
                filter: "[PublicSlug] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_PublicSlug",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PublicSlug",
                table: "UserProfiles");
        }
    }
}
