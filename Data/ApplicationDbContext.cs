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
        }
    }
}
