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

            builder.Entity<TaskItem>()
                .HasOne(task => task.ScheduleItem)
                .WithMany(schedule => schedule.Tasks)
                .HasForeignKey(task => task.ScheduleItemId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
