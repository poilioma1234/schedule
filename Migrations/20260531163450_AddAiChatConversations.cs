using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace schedule.Migrations
{
    /// <inheritdoc />
    public partial class AddAiChatConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConversationId",
                table: "AiChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiChatConversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiChatConversations", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO [AiChatConversations] ([UserId], [UserEmail], [Title], [CreatedAt], [UpdatedAt])
                SELECT
                    [UserId],
                    MAX([UserEmail]),
                    N'Lịch sử AI cũ',
                    MIN([CreatedAt]),
                    MAX([CreatedAt])
                FROM [AiChatMessages]
                WHERE [ConversationId] IS NULL
                GROUP BY [UserId];

                UPDATE [message]
                SET [ConversationId] = [conversation].[Id]
                FROM [AiChatMessages] AS [message]
                INNER JOIN [AiChatConversations] AS [conversation]
                    ON [conversation].[UserId] = [message].[UserId]
                    AND [conversation].[Title] = N'Lịch sử AI cũ'
                WHERE [message].[ConversationId] IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AiChatMessages_ConversationId",
                table: "AiChatMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_AiChatMessages_UserId_ConversationId_CreatedAt",
                table: "AiChatMessages",
                columns: new[] { "UserId", "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiChatConversations_UserId_UpdatedAt",
                table: "AiChatConversations",
                columns: new[] { "UserId", "UpdatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_AiChatMessages_AiChatConversations_ConversationId",
                table: "AiChatMessages",
                column: "ConversationId",
                principalTable: "AiChatConversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiChatMessages_AiChatConversations_ConversationId",
                table: "AiChatMessages");

            migrationBuilder.DropTable(
                name: "AiChatConversations");

            migrationBuilder.DropIndex(
                name: "IX_AiChatMessages_ConversationId",
                table: "AiChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_AiChatMessages_UserId_ConversationId_CreatedAt",
                table: "AiChatMessages");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "AiChatMessages");
        }
    }
}
