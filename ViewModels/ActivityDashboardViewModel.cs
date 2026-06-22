using System;
using System.Collections.Generic;

namespace schedule.ViewModels
{
    public class ActivityDashboardViewModel
    {
        public string UserEmail { get; set; } = string.Empty;

        // KPI Metrics
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public double CompletionRate { get; set; }
        public int CurrentStreakDays { get; set; }
        public int BestStreakDays { get; set; }

        // Comparison metrics (previous period)
        public int PrevTotalTasks { get; set; }
        public int PrevCompletedTasks { get; set; }
        public int PrevOverdueTasks { get; set; }
        public double PrevCompletionRate { get; set; }

        // Comparison percentage differences
        public double TotalTasksDiff { get; set; }
        public double CompletedTasksDiff { get; set; }
        public double OverdueTasksDiff { get; set; }
        public double CompletionRateDiff { get; set; }

        // Donut Chart status distribution (created in period)
        public int DonutCompleted { get; set; }
        public int DonutInProgress { get; set; }
        public int DonutPending { get; set; }
        public int DonutOverdue { get; set; }

        // Combo Chart: Xu hướng hoạt động
        public List<string> MainChartLabels { get; set; } = new();
        public List<int> MainChartCreated { get; set; } = new();
        public List<int> MainChartCompleted { get; set; } = new();
        public List<int> MainChartOverdue { get; set; } = new();

        // Secondary Chart 1: Năng suất theo nhóm thời gian
        public List<string> GroupChartLabels { get; set; } = new();
        public List<int> GroupChartCompleted { get; set; } = new();
        public string GroupChartAverageText { get; set; } = string.Empty;

        // Secondary Chart 2: Xu hướng hoàn thành tích lũy
        public List<string> CumulativeChartLabels { get; set; } = new();
        public List<int> CumulativeChartValues { get; set; } = new();

        // Sparklines for previous period side panel
        public List<int> PrevCreatedTrend { get; set; } = new();
        public List<int> PrevCompletedTrend { get; set; } = new();
        public List<int> PrevOverdueTrend { get; set; } = new();

        // Bottom Detail Table
        public List<PeriodDetailItem> TableDetails { get; set; } = new();
    }

    public class PeriodDetailItem
    {
        public string Label { get; set; } = string.Empty;
        public int CreatedCount { get; set; }
        public int CompletedCount { get; set; }
        public int OverdueCount { get; set; }
        public double CompletionRate { get; set; }
        public double RateChangeComparedToPrev { get; set; } // diff in completion rate vs previous interval
    }
}
