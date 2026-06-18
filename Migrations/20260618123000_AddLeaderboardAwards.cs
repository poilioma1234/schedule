using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using schedule.Data;

#nullable disable

namespace schedule.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260618123000_AddLeaderboardAwards")]
    public partial class AddLeaderboardAwards : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaderboardAwards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserEmailSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayNameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Period = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    CompletedTaskCount = table.Column<int>(type: "int", nullable: false),
                    OnTimeTaskCount = table.Column<int>(type: "int", nullable: false),
                    AwardedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardAwards", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardAwards_Period_PeriodStart_Rank",
                table: "LeaderboardAwards",
                columns: new[] { "Period", "PeriodStart", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardAwards_UserId_Period_PeriodStart",
                table: "LeaderboardAwards",
                columns: new[] { "UserId", "Period", "PeriodStart" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaderboardAwards");
        }
    }
}
