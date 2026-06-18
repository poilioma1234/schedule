using schedule.Models;

namespace schedule.Helpers
{
    public static class LeaderboardHelper
    {
        public static int PriorityScore(TaskPriorityLevel priority)
        {
            return priority switch
            {
                TaskPriorityLevel.Low => 1,
                TaskPriorityLevel.Medium => 2,
                TaskPriorityLevel.High => 3,
                TaskPriorityLevel.Urgent => 5,
                _ => 1
            };
        }

        public static int TaskScore(TaskItem task)
        {
            var onTimeBonus = task.UpdatedAt <= task.Deadline ? 1 : 0;
            return PriorityScore(task.Priority) + onTimeBonus;
        }
    }
}
