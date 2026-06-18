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
        public const string AdminEmail = "tungnt14032004@gmail.com";
        public const string AdminPassword = "123456";
        public const string SampleUserPassword = "123456";

        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

            // Clean up legacy users with @example.com or old admin@example.com
            var legacyUsers = userManager.Users.ToList().Where(u => u.Email != null && (u.Email.Contains("@example.com") || u.Email == "admin@example.com")).ToList();
            if (legacyUsers.Any())
            {
                foreach (var oldUser in legacyUsers)
                {
                    var tasks = context.TaskItems.Where(t => t.CreatedByUserId == oldUser.Id);
                    context.TaskItems.RemoveRange(tasks);

                    var schedules = context.ScheduleItems.Where(s => s.CreatedByUserId == oldUser.Id);
                    context.ScheduleItems.RemoveRange(schedules);

                    var profiles = context.UserProfiles.Where(p => p.UserId == oldUser.Id);
                    context.UserProfiles.RemoveRange(profiles);

                    var reports = context.UserReports.Where(r => r.ReporterUserId == oldUser.Id || r.ReportedUserId == oldUser.Email);
                    context.UserReports.RemoveRange(reports);

                    await context.SaveChangesAsync();
                    await userManager.DeleteAsync(oldUser);
                }
            }

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

            await EnsureTemplateDataForRegularUsersAsync(context, userManager);
            await context.SaveChangesAsync();
        }

        private static async Task EnsureSampleUsersAsync(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            var samples = new[]
            {
                new SampleUserSeed(
                    Email: "nguyenxuancuong999x@gmail.com",
                    DisplayName: "Nguyễn Xuân Cường",
                    Slug: "nguyen-xuan-cuong",
                    Bio: "Sinh viên thích chia nhỏ deadline thành từng bước nhỏ để dễ theo dõi.",
                    MusicUrl: "https://www.youtube.com/watch?v=jfKfPfyJRdk",
                    FacebookUrl: "https://facebook.com/cuongnx",
                    YouTubeUrl: "https://youtube.com/@cuongnx"),
                new SampleUserSeed(
                    Email: "vtd1406@gmail.com",
                    DisplayName: "Vũ Tiến Đạt",
                    Slug: "vu-tien-dat",
                    Bio: "Ưu tiên lịch học, bài tập nhóm và các mốc nộp báo cáo.",
                    MusicUrl: "https://www.youtube.com/watch?v=5qap5aO4i9A",
                    FacebookUrl: "https://facebook.com/vtdat",
                    TikTokUrl: "https://tiktok.com/@vtdat"),
                new SampleUserSeed(
                    Email: "tungnt14062004@gmail.com",
                    DisplayName: "Nguyễn Thanh Tùng",
                    Slug: "nguyen-thanh-tung",
                    Bio: "Dùng Schedule Manager để cân bằng học tập, dự án cá nhân và thời gian nghỉ.",
                    MusicUrl: "https://www.youtube.com/watch?v=DWcJFNfaw9c",
                    YouTubeUrl: "https://youtube.com/@tungnt",
                    WebsiteUrl: "https://example.com/tung-nt"),
                new SampleUserSeed(
                    Email: "hungquadeptrai5@gmail.com",
                    DisplayName: "Hùng Đẹp Trai",
                    Slug: "hung-dep-trai",
                    Bio: "Theo dõi deadline theo tuần và đánh dấu những lịch thật sự quan trọng.",
                    MusicUrl: "https://www.youtube.com/watch?v=jfKfPfyJRdk",
                    FacebookUrl: "https://facebook.com/hungquadeptrai",
                    WebsiteUrl: "https://example.com/hung-dep-trai"),
                new SampleUserSeed(
                    Email: "abcxyz07055@gmail.com",
                    DisplayName: "Nguyễn Anh Tuấn",
                    Slug: "nguyen-anh-tuan",
                    Bio: "Thích lập kế hoạch chi tiết cho từng ngày học tập và giải trí.",
                    MusicUrl: "https://www.youtube.com/watch?v=jfKfPfyJRdk",
                    FacebookUrl: "https://facebook.com/anhtuan"),
                new SampleUserSeed(
                    Email: "nhantt1007@gmail.com",
                    DisplayName: "Trần Thanh Nhân",
                    Slug: "tran-thanh-nhan",
                    Bio: "Tập trung cao độ vào các task quan trọng để hoàn thành sớm nhất.",
                    MusicUrl: "https://www.youtube.com/watch?v=5qap5aO4i9A",
                    FacebookUrl: "https://facebook.com/thanhnhan")
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
                    IsProfilePublic = true,
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
            profile.IsProfilePublic = true;
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

        private static async Task EnsureTemplateDataForRegularUsersAsync(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager)
        {
            var users = await userManager.GetUsersInRoleAsync("User");
            var templateStart = new DateTime(2026, 5, 4);
            var templateEnd = new DateTime(2026, 7, 21);
            var today = DateTime.Today;
            var topics = new[]
            {
                "Ôn tập môn học",
                "Deadline bài tập",
                "Làm báo cáo nhóm",
                "Chuẩn bị demo",
                "Đọc tài liệu",
                "Review kế hoạch",
                "Cập nhật project",
                "Kiểm tra tiến độ",
                "Lập trình ASP.NET Core",
                "Luyện thi chứng chỉ",
                "Thiết kế database"
            };
            var locations = new[] { "Thư viện", "Ở nhà", "Online", "Phòng lab", "Quán cà phê", "Lớp học" };

            foreach (var user in users.Where(user => user.Email != AdminEmail))
            {
                // Delete existing template tasks & schedules first to allow clean re-seed
                var oldSchedules = await context.ScheduleItems
                    .Where(item => item.CreatedByUserId == user.Id && item.Title.StartsWith("[Template]"))
                    .ToListAsync();
                if (oldSchedules.Any())
                {
                    context.ScheduleItems.RemoveRange(oldSchedules);
                    await context.SaveChangesAsync();
                }

                var seed = Math.Abs((user.Email ?? user.Id).GetHashCode());
                var random = new Random(seed);

                // Set completion rate based on user email
                double completionRate = user.Email switch
                {
                    "nguyenxuancuong999x@gmail.com" => 1.0,
                    "vtd1406@gmail.com" => 0.8,
                    "tungnt14062004@gmail.com" => 0.7,
                    "hungquadeptrai5@gmail.com" => 0.9,
                    "abcxyz07055@gmail.com" => 0.5,
                    "nhantt1007@gmail.com" => 0.85,
                    _ => 0.8
                };

                for (var date = templateStart.Date; date <= templateEnd.Date; date = date.AddDays(1))
                {
                    // Target total tasks for this user on this day: between 5 and 20
                    var totalTasksForDay = random.Next(5, 21);
                    var tasksRemaining = totalTasksForDay;
                    var schedIndex = 1;

                    while (tasksRemaining > 0)
                    {
                        var topic = topics[random.Next(topics.Length)];
                        var startHour = 8 + (schedIndex * 3) % 12; // spread them out
                        var startTime = date.AddHours(startHour).AddMinutes(random.Next(0, 2) * 30);
                        var schedule = new ScheduleItem
                        {
                            Title = $"[Template] {topic} - {date:dd/MM} (Ca {schedIndex})",
                            Description = "Dữ liệu lịch trình mẫu để kiểm tra hệ thống.",
                            StartTime = startTime,
                            EndTime = startTime.AddHours(random.Next(1, 3)),
                            Location = locations[random.Next(locations.Length)],
                            IsImportant = random.NextDouble() < 0.3,
                            ReceiverEmail = user.Email,
                            ReminderMinutes = 5,
                            CreatedByUserId = user.Id,
                            CreatedByEmail = user.Email,
                            CreatedAt = date.AddDays(-random.Next(1, 5)).AddHours(9)
                        };

                        context.ScheduleItems.Add(schedule);

                        // Assign between 2 and 6 tasks to this schedule
                        var tasksForThisSched = Math.Min(random.Next(2, 6), tasksRemaining);
                        tasksRemaining -= tasksForThisSched;

                        for (var index = 1; index <= tasksForThisSched; index++)
                        {
                            var priority = (TaskPriorityLevel)random.Next(0, 4);
                            var deadline = date.AddHours(random.Next(9, 23));

                            TaskItemStatus status;
                            DateTime completedAt;

                            if (date.Date < today.Date)
                            {
                                // Past date: status is either Completed or Overdue based on completionRate
                                if (random.NextDouble() < completionRate)
                                {
                                    status = TaskItemStatus.Completed;
                                    completedAt = date.AddHours(10 + random.Next(0, 10));
                                }
                                else
                                {
                                    status = TaskItemStatus.Overdue;
                                    completedAt = date.AddHours(9);
                                }
                            }
                            else if (date.Date == today.Date)
                            {
                                // Today: mix of Completed, InProgress, and Overdue (if deadline is past)
                                if (random.NextDouble() < completionRate)
                                {
                                    status = TaskItemStatus.Completed;
                                    completedAt = DateTime.Now.AddHours(-1);
                                }
                                else
                                {
                                    status = deadline < DateTime.Now ? TaskItemStatus.Overdue : TaskItemStatus.InProgress;
                                    completedAt = date.AddHours(9);
                                }
                            }
                            else
                            {
                                // Future date: InProgress or NotStarted
                                status = random.NextDouble() < 0.35 ? TaskItemStatus.InProgress : TaskItemStatus.NotStarted;
                                completedAt = date.AddHours(9);
                            }

                            context.TaskItems.Add(new TaskItem
                            {
                                ScheduleItem = schedule,
                                Title = $"[Template] Task {schedIndex}-{index}: {topic}",
                                Description = "Task mẫu có deadline, trạng thái, màu và mức độ ưu tiên.",
                                Deadline = deadline,
                                Status = status,
                                Priority = priority,
                                Color = TaskDisplayHelper.PriorityColor(priority),
                                AttachmentUrl = random.NextDouble() < 0.45 ? "https://example.com/template-task" : null,
                                CreatedByUserId = user.Id,
                                CreatedByEmail = user.Email,
                                CreatedAt = schedule.CreatedAt,
                                UpdatedAt = completedAt
                            });
                        }
                        schedIndex++;
                    }
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
                        DisplayName = user.Email ?? user.UserName ?? "User",
                        IsProfilePublic = true
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
