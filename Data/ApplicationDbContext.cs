using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using schedule.Models;

namespace schedule.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<ScheduleItem> ScheduleItems => Set<ScheduleItem>();
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<TaskItem> TaskItems => Set<TaskItem>();
        public DbSet<AiChatConversation> AiChatConversations => Set<AiChatConversation>();
        public DbSet<AiChatMessage> AiChatMessages => Set<AiChatMessage>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<UserProfile>()
                .HasIndex(profile => profile.UserId)
                .IsUnique();

            builder.Entity<UserProfile>()
                .HasIndex(profile => profile.PublicSlug)
                .IsUnique()
                .HasFilter("[PublicSlug] IS NOT NULL");

            builder.Entity<UserProfile>()
                .Property(profile => profile.IsProfilePublic)
                .HasDefaultValue(true);

            builder.Entity<TaskItem>()
                .HasOne(task => task.ScheduleItem)
                .WithMany(schedule => schedule.Tasks)
                .HasForeignKey(task => task.ScheduleItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AiChatMessage>()
                .HasIndex(message => new { message.UserId, message.CreatedAt });

            builder.Entity<AiChatConversation>()
                .HasIndex(conversation => new { conversation.UserId, conversation.UpdatedAt });

            builder.Entity<AiChatMessage>()
                .HasIndex(message => new { message.UserId, message.ConversationId, message.CreatedAt });

            builder.Entity<AiChatConversation>()
                .HasMany(conversation => conversation.Messages)
                .WithOne(message => message.Conversation)
                .HasForeignKey(message => message.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
