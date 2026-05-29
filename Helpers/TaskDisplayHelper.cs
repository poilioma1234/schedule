using schedule.Models;

namespace schedule.Helpers
{
    public static class TaskDisplayHelper
    {
        public static string StatusText(TaskItemStatus status)
        {
            return status switch
            {
                TaskItemStatus.NotStarted => "Chưa làm",
                TaskItemStatus.InProgress => "Đang làm",
                TaskItemStatus.Completed => "Hoàn thành",
                TaskItemStatus.Overdue => "Quá hạn",
                _ => "Chưa làm"
            };
        }

        public static string PriorityText(TaskPriorityLevel priority)
        {
            return priority switch
            {
                TaskPriorityLevel.Low => "Thấp",
                TaskPriorityLevel.Medium => "Vừa",
                TaskPriorityLevel.High => "Cao",
                TaskPriorityLevel.Urgent => "Khẩn cấp",
                _ => "Vừa"
            };
        }

        public static string PriorityColor(TaskPriorityLevel priority)
        {
            return priority switch
            {
                TaskPriorityLevel.Low => "#0f766e",
                TaskPriorityLevel.Medium => "#0d6efd",
                TaskPriorityLevel.High => "#f59e0b",
                TaskPriorityLevel.Urgent => "#dc2626",
                _ => "#0d6efd"
            };
        }

        public static TaskItemStatus EffectiveStatus(TaskItem task, DateTime now)
        {
            if (task.Status == TaskItemStatus.Completed)
            {
                return TaskItemStatus.Completed;
            }

            if (task.Status == TaskItemStatus.Overdue || task.Deadline < now)
            {
                return TaskItemStatus.Overdue;
            }

            return task.Status;
        }
    }
}
