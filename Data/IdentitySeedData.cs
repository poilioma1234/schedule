using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using schedule.Helpers;
using schedule.Models;

namespace schedule.Data
{
    public static class IdentitySeedData
    {
        public const string AdminEmail = "admin@example.com";
        public const string AdminPassword = "Admin@123";
        public const string SampleUserPassword = "User@123";

        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

            foreach (var role in new[] { "Admin", "User" })
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            var admin = await userManager.FindByEmailAsync(AdminEmail);
            if (admin == null)
            {
                admin = new IdentityUser
                {
                    UserName = AdminEmail,
                    Email = AdminEmail,
                    EmailConfirmed = true
                };

                await userManager.CreateAsync(admin, AdminPassword);
            }

            if (!await userManager.IsInRoleAsync(admin, "Admin"))
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }

            await EnsureSampleUsersAsync(context, userManager);

            var usersWithoutRole = userManager.Users.ToList();
            foreach (var user in usersWithoutRole)
            {
                var roles = await userManager.GetRolesAsync(user);
                if (!roles.Any() && user.Email != AdminEmail)
                {
                    await userManager.AddToRoleAsync(user, "User");
                }
            }

            await EnsureProfilesForExistingUsersAsync(context, userManager);
            await context.SaveChangesAsync();
        }

        private static async Task EnsureSampleUsersAsync(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            var samples = new[]
            {
                new SampleUserSeed(
                    Email: "minhanh@example.com",
                    DisplayName: "Minh Anh",
                    Slug: "minh-anh",
                    Bio: "Sinh viên thích chia nhỏ deadline thành từng bước nhỏ để dễ theo dõi.",
                    MusicUrl: "https://www.youtube.com/watch?v=jfKfPfyJRdk",
                    FacebookUrl: "https://facebook.com/minhanh",
                    YouTubeUrl: "https://youtube.com/@minhanh"),
                new SampleUserSeed(
                    Email: "hoangnam@example.com",
                    DisplayName: "Hoàng Nam",
                    Slug: "hoang-nam",
                    Bio: "Ưu tiên lịch học, bài tập nhóm và các mốc nộp báo cáo.",
                    MusicUrl: "https://www.youtube.com/watch?v=5qap5aO4i9A",
                    FacebookUrl: "https://facebook.com/hoangnam",
                    TikTokUrl: "https://tiktok.com/@hoangnam"),
                new SampleUserSeed(
                    Email: "lanchi@example.com",
                    DisplayName: "Lan Chi",
                    Slug: "lan-chi",
                    Bio: "Dùng Schedule Manager để cân bằng học tập, dự án cá nhân và thời gian nghỉ.",
                    MusicUrl: "https://www.youtube.com/watch?v=DWcJFNfaw9c",
                    YouTubeUrl: "https://youtube.com/@lanchi",
                    WebsiteUrl: "https://example.com/lan-chi"),
                new SampleUserSeed(
                    Email: "ducminh@example.com",
                    DisplayName: "Đức Minh",
                    Slug: "duc-minh",
                    Bio: "Theo dõi deadline theo tuần và đánh dấu những lịch thật sự quan trọng.",
                    MusicUrl: "https://www.youtube.com/watch?v=jfKfPfyJRdk",
                    FacebookUrl: "https://facebook.com/ducminh",
                    WebsiteUrl: "https://example.com/duc-minh")
            };

            foreach (var sample in samples)
            {
                var user = await userManager.FindByEmailAsync(sample.Email);
                if (user == null)
                {
                    user = new IdentityUser
                    {
                        UserName = sample.Email,
                        Email = sample.Email,
                        EmailConfirmed = true
                    };

                    await userManager.CreateAsync(user, SampleUserPassword);
                }

                if (!await userManager.IsInRoleAsync(user, "User"))
                {
                    await userManager.AddToRoleAsync(user, "User");
                }

                await EnsureSampleProfileAsync(context, user, sample);
                await EnsureSampleSchedulesAsync(context, user, sample.DisplayName);
            }

            await context.SaveChangesAsync();

            foreach (var sample in samples)
            {
                var user = await userManager.FindByEmailAsync(sample.Email);
                if (user != null)
                {
                    await EnsureSampleTasksAsync(context, user);
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task EnsureSampleProfileAsync(
            ApplicationDbContext context,
            IdentityUser user,
            SampleUserSeed sample)
        {
            var profile = await context.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id);
            if (profile == null)
            {
                context.UserProfiles.Add(new UserProfile
                {
                    UserId = user.Id,
                    DisplayName = sample.DisplayName,
                    PublicSlug = sample.Slug,
                    Bio = sample.Bio,
                    MusicUrl = sample.MusicUrl,
                    FacebookUrl = sample.FacebookUrl,
                    YouTubeUrl = sample.YouTubeUrl,
                    TikTokUrl = sample.TikTokUrl,
                    WebsiteUrl = sample.WebsiteUrl
                });

                return;
            }

            if (string.IsNullOrWhiteSpace(profile.PublicSlug))
            {
                profile.PublicSlug = sample.Slug;
            }

            if (profile.DisplayName == user.Email || string.IsNullOrWhiteSpace(profile.DisplayName))
            {
                profile.DisplayName = sample.DisplayName;
            }

            profile.Bio = sample.Bio;
            profile.MusicUrl = sample.MusicUrl;
            profile.FacebookUrl = sample.FacebookUrl;
            profile.YouTubeUrl = sample.YouTubeUrl;
            profile.TikTokUrl = sample.TikTokUrl;
            profile.WebsiteUrl = sample.WebsiteUrl;
        }

        private static async Task EnsureSampleSchedulesAsync(
            ApplicationDbContext context,
            IdentityUser user,
            string displayName)
        {
            if (await context.ScheduleItems.AnyAsync(item => item.CreatedByUserId == user.Id))
            {
                return;
            }

            var today = DateTime.Today;
            var sampleSchedules = new[]
            {
                new ScheduleItem
                {
                    Title = "Ôn tập ASP.NET Core",
                    Description = $"Task mẫu của {displayName}: đọc lại MVC, Identity và EF Core.",
                    StartTime = today.AddHours(8),
                    EndTime = today.AddHours(9),
                    Location = "Thư viện",
                    IsImportant = true
                },
                new ScheduleItem
                {
                    Title = "Hoàn thành báo cáo nhóm",
                    Description = $"Task mẫu của {displayName}: chốt nội dung và gửi bản cuối.",
                    StartTime = today.AddDays(1).AddHours(14),
                    EndTime = today.AddDays(1).AddHours(16),
                    Location = "Online",
                    IsImportant = true
                },
                new ScheduleItem
                {
                    Title = "Kiểm tra deadline cá nhân",
                    Description = $"Task mẫu của {displayName}: rà lại các việc còn mở trong tuần.",
                    StartTime = today.AddDays(2).AddHours(19),
                    EndTime = today.AddDays(2).AddHours(20),
                    Location = "Ở nhà",
                    IsImportant = false
                },
                new ScheduleItem
                {
                    Title = "Chuẩn bị demo project",
                    Description = $"Task mẫu của {displayName}: kiểm tra dữ liệu, giao diện và kịch bản trình bày.",
                    StartTime = today.AddDays(4).AddHours(9),
                    EndTime = today.AddDays(4).AddHours(11),
                    Location = "Phòng lab",
                    IsImportant = true
                },
                new ScheduleItem
                {
                    Title = "Review kế hoạch tuần",
                    Description = $"Task mẫu của {displayName}: xem lại tiến độ và điều chỉnh lịch.",
                    StartTime = today.AddDays(-1).AddHours(20),
                    EndTime = today.AddDays(-1).AddHours(21),
                    Location = "Cá nhân",
                    IsImportant = false,
                    ReminderSentAt = today.AddDays(-1).AddHours(19).AddMinutes(55)
                }
            };

            foreach (var item in sampleSchedules)
            {
                item.CreatedByUserId = user.Id;
                item.CreatedByEmail = user.Email;
                item.ReceiverEmail = user.Email;
                item.ReminderMinutes = 5;
                item.CreatedAt = item.StartTime.AddDays(-3);
            }

            context.ScheduleItems.AddRange(sampleSchedules);
        }

        private static async Task EnsureSampleTasksAsync(ApplicationDbContext context, IdentityUser user)
        {
            if (await context.TaskItems.AnyAsync(item => item.CreatedByUserId == user.Id))
            {
                return;
            }

            var schedules = await context.ScheduleItems
                .Where(item => item.CreatedByUserId == user.Id)
                .OrderBy(item => item.StartTime)
                .Take(4)
                .ToListAsync();

            foreach (var schedule in schedules)
            {
                var taskSeeds = new[]
                {
                    new
                    {
                        Title = "Chuẩn bị nội dung chính",
                        Description = "Ghi ra các ý quan trọng cần hoàn thành trước lịch.",
                        Deadline = schedule.StartTime.AddHours(-2),
                        Status = schedule.StartTime < DateTime.Now ? TaskItemStatus.Completed : TaskItemStatus.InProgress,
                        Priority = TaskPriorityLevel.High,
                        AttachmentUrl = "https://docs.google.com"
                    },
                    new
                    {
                        Title = "Kiểm tra tài liệu đính kèm",
                        Description = "Rà lại file, link và các ghi chú liên quan.",
                        Deadline = schedule.StartTime.AddHours(-1),
                        Status = schedule.StartTime < DateTime.Now ? TaskItemStatus.Completed : TaskItemStatus.NotStarted,
                        Priority = TaskPriorityLevel.Medium,
                        AttachmentUrl = "https://drive.google.com"
                    },
                    new
                    {
                        Title = "Chốt kết quả sau lịch",
                        Description = "Cập nhật phần đã làm xong và việc còn lại.",
                        Deadline = schedule.EndTime.AddHours(2),
                        Status = schedule.EndTime < DateTime.Now ? TaskItemStatus.Overdue : TaskItemStatus.NotStarted,
                        Priority = schedule.IsImportant ? TaskPriorityLevel.Urgent : TaskPriorityLevel.Low,
                        AttachmentUrl = "https://example.com/task-note"
                    }
                };

                foreach (var seed in taskSeeds)
                {
                    context.TaskItems.Add(new TaskItem
                    {
                        ScheduleItemId = schedule.Id,
                        Title = seed.Title,
                        Description = seed.Description,
                        Deadline = seed.Deadline,
                        Status = seed.Status,
                        Priority = seed.Priority,
                        Color = TaskDisplayHelper.PriorityColor(seed.Priority),
                        AttachmentUrl = seed.AttachmentUrl,
                        CreatedByUserId = user.Id,
                        CreatedByEmail = user.Email,
                        CreatedAt = schedule.CreatedAt,
                        UpdatedAt = DateTime.Now
                    });
                }
            }
        }

        private static async Task EnsureProfilesForExistingUsersAsync(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager)
        {
            var profiles = await context.UserProfiles.ToListAsync();
            var usedSlugs = profiles
                .Where(profile => !string.IsNullOrWhiteSpace(profile.PublicSlug))
                .Select(profile => profile.PublicSlug!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var user in userManager.Users.ToList())
            {
                var profile = profiles.FirstOrDefault(item => item.UserId == user.Id);
                if (profile == null)
                {
                    profile = new UserProfile
                    {
                        UserId = user.Id,
                        DisplayName = user.Email ?? user.UserName ?? "User"
                    };

                    context.UserProfiles.Add(profile);
                    profiles.Add(profile);
                }

                if (string.IsNullOrWhiteSpace(profile.PublicSlug))
                {
                    profile.PublicSlug = CreateUniqueSlug(user.Email ?? user.UserName ?? "user", usedSlugs);
                    usedSlugs.Add(profile.PublicSlug);
                }
            }
        }

        private static string CreateUniqueSlug(string value, HashSet<string> usedSlugs)
        {
            var baseSlug = Slugify(value.Split('@')[0]);
            var candidate = baseSlug;
            var counter = 2;

            while (usedSlugs.Contains(candidate))
            {
                candidate = $"{baseSlug}-{counter}";
                counter++;
            }

            return candidate;
        }

        private static string Slugify(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();

            foreach (var character in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            var slug = Regex.Replace(builder.ToString().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            return string.IsNullOrWhiteSpace(slug) ? "user" : slug[..Math.Min(slug.Length, 80)];
        }

        private sealed record SampleUserSeed(
            string Email,
            string DisplayName,
            string Slug,
            string Bio,
            string MusicUrl,
            string? FacebookUrl = null,
            string? YouTubeUrl = null,
            string? TikTokUrl = null,
            string? WebsiteUrl = null);
    }
}
