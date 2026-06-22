using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace schedule.Migrations
{
    /// <inheritdoc />
    public partial class AddOverdueEmailSentAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueEmailSentAt",
                table: "TaskItems",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverdueEmailSentAt",
                table: "TaskItems");
        }
    }
}
