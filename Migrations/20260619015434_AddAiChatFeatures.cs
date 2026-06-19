using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace schedule.Migrations
{
    /// <inheritdoc />
    public partial class AddAiChatFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[AiChatConversations]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [AiChatConversations] (
                        [Id] int NOT NULL IDENTITY,
                        [UserId] nvarchar(450) NOT NULL,
                        [UserEmail] nvarchar(256) NULL,
                        [Title] nvarchar(160) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_AiChatConversations] PRIMARY KEY ([Id])
                    );
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[AiChatMessages]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [AiChatMessages] (
                        [Id] int NOT NULL IDENTITY,
                        [ConversationId] int NULL,
                        [UserId] nvarchar(450) NOT NULL,
                        [UserEmail] nvarchar(256) NULL,
                        [Role] nvarchar(20) NOT NULL,
                        [Content] nvarchar(4000) NOT NULL,
                        [PlanJson] nvarchar(max) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_AiChatMessages] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_AiChatMessages_AiChatConversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [AiChatConversations] ([Id]) ON DELETE CASCADE
                    );
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_AiChatConversations_UserId_UpdatedAt'
                      AND object_id = OBJECT_ID(N'[AiChatConversations]')
                )
                BEGIN
                    CREATE INDEX [IX_AiChatConversations_UserId_UpdatedAt]
                    ON [AiChatConversations] ([UserId], [UpdatedAt]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_AiChatMessages_ConversationId'
                      AND object_id = OBJECT_ID(N'[AiChatMessages]')
                )
                BEGIN
                    CREATE INDEX [IX_AiChatMessages_ConversationId]
                    ON [AiChatMessages] ([ConversationId]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_AiChatMessages_UserId_ConversationId_CreatedAt'
                      AND object_id = OBJECT_ID(N'[AiChatMessages]')
                )
                BEGIN
                    CREATE INDEX [IX_AiChatMessages_UserId_ConversationId_CreatedAt]
                    ON [AiChatMessages] ([UserId], [ConversationId], [CreatedAt]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_AiChatMessages_UserId_CreatedAt'
                      AND object_id = OBJECT_ID(N'[AiChatMessages]')
                )
                BEGIN
                    CREATE INDEX [IX_AiChatMessages_UserId_CreatedAt]
                    ON [AiChatMessages] ([UserId], [CreatedAt]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[AiChatMessages]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [AiChatMessages];
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[AiChatConversations]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [AiChatConversations];
                END
                """);
        }
    }
}
