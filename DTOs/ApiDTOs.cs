using System;
using System.Collections.Generic;
using schedule.Models;

namespace schedule.DTOs
{
    public class ScheduleItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string? Location { get; set; }
        public bool IsImportant { get; set; }
        public string? ReceiverEmail { get; set; }
        public int ReminderMinutes { get; set; }
        public string? CreatedByUserId { get; set; }
        public string? CreatedByEmail { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<TaskItemDto> Tasks { get; set; } = new();
    }

    public class ScheduleItemCreateDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartTime { get; set; } = DateTime.Today.AddHours(8);
        public DateTime EndTime { get; set; } = DateTime.Today.AddHours(9);
        public string? Location { get; set; }
        public bool IsImportant { get; set; }
        public string? ReceiverEmail { get; set; }
        public int ReminderMinutes { get; set; } = 5;
    }

    public class TaskItemDto
    {
        public int Id { get; set; }
        public int ScheduleItemId { get; set; }
        public string? ScheduleItemTitle { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime Deadline { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string? AttachmentUrl { get; set; }
        public string? CreatedByUserId { get; set; }
        public string? CreatedByEmail { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TaskItemCreateDto
    {
        public int ScheduleItemId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime Deadline { get; set; } = DateTime.Today.AddHours(17);
        public TaskItemStatus Status { get; set; } = TaskItemStatus.NotStarted;
        public TaskPriorityLevel Priority { get; set; } = TaskPriorityLevel.Medium;
        public string Color { get; set; } = "#0d6efd";
        public string? AttachmentUrl { get; set; }
    }

    public class TaskItemUpdateStatusDto
    {
        public TaskItemStatus Status { get; set; }
    }

    public class LeaderboardEntryDto
    {
        public int Rank { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public int ExperiencePoints { get; set; }
        public bool IsCurrentUser { get; set; }
    }

    public class AiChatMessageDto
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class AiChatConversationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UserProfileDto
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? AvatarPath { get; set; }
        public string? CoverPath { get; set; }
        public string? PublicSlug { get; set; }
        public bool IsProfilePublic { get; set; }
        public string? MusicUrl { get; set; }
        public string? FacebookUrl { get; set; }
        public string? YouTubeUrl { get; set; }
        public string? TikTokUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? Email { get; set; }
        public int TotalSchedules { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTaskCount { get; set; }
        public int OverdueTaskCount { get; set; }
        public int CurrentStreakDays { get; set; }
        public int LongestStreakDays { get; set; }
        public string RankLabel { get; set; } = string.Empty;
    }

    public class UserProfileUpdateDto
    {
        public string? DisplayName { get; set; }
        public string? Bio { get; set; }
        public bool IsProfilePublic { get; set; }
        public string? MusicUrl { get; set; }
        public string? FacebookUrl { get; set; }
        public string? YouTubeUrl { get; set; }
        public string? TikTokUrl { get; set; }
        public string? WebsiteUrl { get; set; }
    }

    public class UserReportCreateDto
    {
        public string ReportedUserId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class AdminUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public string Roles { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public bool IsLocked { get; set; }
        public int ScheduleCount { get; set; }
        public int TodayScheduleCount { get; set; }
        public int ActiveOrUpcomingScheduleCount { get; set; }
        public int TotalTaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
        public int OverdueTaskCount { get; set; }
        public DateTime? LastScheduleAt { get; set; }
    }

    public class AdminReportDto
    {
        public int Id { get; set; }
        public string ReportedUserId { get; set; } = string.Empty;
        public string? ReportedUserEmail { get; set; }
        public string? ReporterUserId { get; set; }
        public string? ReporterUserEmail { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? AdminNote { get; set; }
        public DateTime? HandledAt { get; set; }
    }

    public class ResolveReportRequestDto
    {
        public string? AdminNote { get; set; }
    }
}
