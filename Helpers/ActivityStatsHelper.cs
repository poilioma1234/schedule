using schedule.Models;
using schedule.ViewModels;

namespace schedule.Helpers
{
    public static class ActivityStatsHelper
    {
        public static ActivityChartViewModel BuildCompletedTasksByDay(
            IEnumerable<TaskItem> tasks,
            DateTime endDate,
            int days)
        {
            var taskList = tasks.ToList();
            var start = endDate.Date.AddDays(-(days - 1));
            var labels = new List<string>();
            var values = new List<int>();

            for (var date = start; date <= endDate.Date; date = date.AddDays(1))
            {
                labels.Add(date.ToString("dd/MM"));
                values.Add(taskList.Count(task => task.Status == TaskItemStatus.Completed && task.UpdatedAt.Date == date.Date));
            }

            return new ActivityChartViewModel
            {
                Title = "Task đã hoàn thành",
                Labels = labels,
                Values = values,
                Color = "#16a34a"
            };
        }

        public static (int Current, int Longest) CalculateCompletionStreak(
            IEnumerable<TaskItem> tasks,
            DateTime today)
        {
            var activityDays = tasks
                .Where(task => task.Status == TaskItemStatus.Completed)
                .Select(task => task.UpdatedAt.Date)
                .Distinct()
                .ToHashSet();

            if (!activityDays.Any())
            {
                return (0, 0);
            }

            var orderedDays = activityDays.OrderBy(day => day).ToList();
            var longest = 1;
            var currentRun = 1;

            for (var index = 1; index < orderedDays.Count; index++)
            {
                if (orderedDays[index] == orderedDays[index - 1].AddDays(1))
                {
                    currentRun++;
                }
                else
                {
                    longest = Math.Max(longest, currentRun);
                    currentRun = 1;
                }
            }

            longest = Math.Max(longest, currentRun);

            var current = CountBackwards(activityDays, today.Date);
            if (current == 0)
            {
                current = CountBackwards(activityDays, today.Date.AddDays(-1));
            }

            return (current, longest);
        }

        private static int CountBackwards(HashSet<DateTime> activityDays, DateTime startDate)
        {
            var count = 0;
            var cursor = startDate.Date;

            while (activityDays.Contains(cursor))
            {
                count++;
                cursor = cursor.AddDays(-1);
            }

            return count;
        }
    }
}
