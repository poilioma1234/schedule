using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using schedule.Models;
using SkiaSharp;

namespace schedule.Helpers
{
    public static class ReportPdfGenerator
    {
        public static byte[] Generate(
            List<ScheduleItem> schedules,
            List<TaskItem> tasks,
            (int Total, int Completed, int Overdue, int InProgress)? activitySummary,
            string ownerEmail,
            DateTime? from,
            DateTime? to,
            bool includeSchedules,
            bool includeTasks,
            bool includeActivity)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(text => text.FontSize(10));

                    // ── Header ──
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(inner =>
                            {
                                var reportTitle = "Báo cáo tổng hợp cá nhân";
                                if (includeSchedules && !includeTasks)
                                {
                                    reportTitle = "Báo cáo lịch trình kèm task";
                                }
                                else if (!includeSchedules && includeTasks)
                                {
                                    reportTitle = "Báo cáo task chi tiết";
                                }
                                else if (!includeSchedules && !includeTasks)
                                {
                                    reportTitle = "Báo cáo hiệu suất cá nhân";
                                }

                                inner.Item().Text($"Schedule Manager – {reportTitle}")
                                    .FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                                inner.Item().Text($"Dữ liệu của: {ownerEmail}").FontSize(11).FontColor(Colors.Grey.Darken1);
                                var dateRange = (from, to) switch
                                {
                                    (null, null) => "Tất cả thời gian",
                                    (var f, null) => $"Từ {f:dd/MM/yyyy}",
                                    (null, var t) => $"Đến {t:dd/MM/yyyy}",
                                    (var f, var t) => $"Từ {f:dd/MM/yyyy} đến {t:dd/MM/yyyy}"
                                };
                                inner.Item().Text($"Thời gian: {dateRange}").FontSize(10).FontColor(Colors.Grey.Medium);
                                inner.Item().Text($"Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Lighten1);
                            });
                        });

                        col.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(Colors.Blue.Lighten3);
                        col.Item().PaddingBottom(4);
                    });

                    page.Content().Column(content =>
                    {
                        // ── Activity Summary ──
                        if (includeActivity && activitySummary.HasValue)
                        {
                            var s = activitySummary.Value;
                            content.Item().PaddingBottom(12).Column(col =>
                            {
                                col.Item().Text("Tóm tắt hoạt động").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                                col.Item().PaddingTop(6).Row(row =>
                                {
                                    void SummaryBox(string label, int value, string bgColor)
                                    {
                                        row.RelativeItem().Padding(4).Background(bgColor).Padding(10).Column(inner =>
                                        {
                                            inner.Item().Text(label).FontSize(9).FontColor(Colors.White);
                                            inner.Item().Text(value.ToString()).FontSize(18).Bold().FontColor(Colors.White);
                                        });
                                    }

                                    SummaryBox("Tổng task", s.Total, Colors.Blue.Darken2);
                                    SummaryBox("Hoàn thành", s.Completed, Colors.Green.Darken2);
                                    SummaryBox("Quá hạn", s.Overdue, Colors.Red.Darken2);
                                    SummaryBox("Đang làm", s.InProgress, Colors.Orange.Darken2);
                                });
                            });
                        }

                        // ── Charts Section ──
                        if (includeActivity && tasks.Count > 0)
                        {
                            content.Item().PaddingBottom(16).Column(col =>
                            {
                                col.Item().Row(row =>
                                {
                                    row.RelativeItem(2).Column(left =>
                                    {
                                        left.Item().Text("Phân bổ trạng thái công việc")
                                            .FontSize(11).Bold().FontColor(Colors.Blue.Darken2);
                                        
                                        var total = tasks.Count;
                                        var completed = tasks.Count(t => t.Status == TaskItemStatus.Completed);
                                        var inProgress = tasks.Count(t => t.Status == TaskItemStatus.InProgress);
                                        var overdue = tasks.Count(t => t.Status != TaskItemStatus.Completed && t.Deadline < DateTime.Now);
                                        var pending = total - completed - inProgress - overdue;
                                        if (pending < 0) pending = 0;

                                        var doughnutBytes = GenerateDoughnutChartImage(completed, inProgress, overdue, pending);
                                        left.Item().PaddingTop(6).Image(doughnutBytes);
                                    });

                                    row.ConstantItem(25); // spacing

                                    row.RelativeItem(3).Column(right =>
                                    {
                                        right.Item().Text("Xu hướng hoàn thành theo ngày")
                                            .FontSize(11).Bold().FontColor(Colors.Blue.Darken2);

                                        var trendBytes = GenerateDailyTrendChartImage(tasks, from, to);
                                        right.Item().PaddingTop(6).Image(trendBytes);
                                    });
                                });
                            });
                        }

                        // ── Hierarchical Schedules & Tasks ──
                        if (includeSchedules && !includeTasks && !includeActivity)
                        {
                            content.Item().PaddingBottom(16).Column(col =>
                            {
                                col.Item().Text("Danh sách lịch trình & Task")
                                    .FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                                
                                if (schedules.Count == 0)
                                {
                                    col.Item().PaddingTop(6).Text("Không có lịch trình phù hợp.").Italic().FontColor(Colors.Grey.Medium);
                                }
                                else
                                {
                                    // Group schedules by start date
                                    var groupedSchedules = schedules
                                        .GroupBy(s => s.StartTime.Date)
                                        .OrderBy(g => g.Key);

                                    foreach (var group in groupedSchedules)
                                    {
                                        col.Item().PaddingTop(10).Column(dayCol =>
                                        {
                                            // Date header
                                            dayCol.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(group.Key.ToString("dd/MM/yyyy"))
                                                .FontSize(11).Bold().FontColor(Colors.Blue.Darken3);

                                            foreach (var item in group)
                                            {
                                                dayCol.Item().PaddingLeft(10).PaddingTop(6).Column(itemCol =>
                                                {
                                                    // Time & Title
                                                    var importantMark = item.IsImportant ? " [QUAN TRỌNG]" : "";
                                                    itemCol.Item().Text($"[{item.StartTime:HH:mm} - {item.EndTime:HH:mm}] {item.Title}{importantMark}")
                                                        .FontSize(10).Bold().FontColor(Colors.Grey.Darken3);

                                                    // Location if exists
                                                    if (!string.IsNullOrWhiteSpace(item.Location))
                                                    {
                                                        itemCol.Item().Text($"Địa điểm: {item.Location}").FontSize(9).FontColor(Colors.Grey.Medium);
                                                    }

                                                    // Summary stats of child tasks
                                                    var childTasks = item.Tasks.ToList();
                                                    var totalChild = childTasks.Count;
                                                    var completedChild = childTasks.Count(t => t.Status == TaskItemStatus.Completed);
                                                    var now = DateTime.Now;
                                                    var overdueChild = childTasks.Count(t => t.Status != TaskItemStatus.Completed && t.Deadline < now);
                                                    
                                                    itemCol.Item().Text($"Tổng task: {totalChild} | Hoàn thành: {completedChild} | Quá hạn: {overdueChild}")
                                                        .FontSize(9).Italic().FontColor(Colors.Blue.Darken2);

                                                    // Child tasks list
                                                    if (childTasks.Count > 0)
                                                    {
                                                        itemCol.Item().PaddingLeft(15).PaddingTop(4).Column(taskListCol =>
                                                        {
                                                            foreach (var task in childTasks)
                                                            {
                                                                var isOverdue = task.Status != TaskItemStatus.Completed && task.Deadline < now;
                                                                var statusSymbol = task.Status switch
                                                                {
                                                                    TaskItemStatus.Completed => "✓",
                                                                    TaskItemStatus.InProgress => "○",
                                                                    _ when isOverdue => "!",
                                                                    _ => "○"
                                                                };
                                                                var color = task.Status switch
                                                                {
                                                                    TaskItemStatus.Completed => Colors.Green.Darken2,
                                                                    _ when isOverdue => Colors.Red.Darken2,
                                                                    _ => Colors.Grey.Darken2
                                                                };
                                                                
                                                                taskListCol.Item().Text($"{statusSymbol}  {task.Title} (Hạn: {task.Deadline:dd/MM HH:mm})")
                                                                    .FontSize(9).FontColor(color);
                                                            }
                                                        });
                                                    }
                                                    else
                                                    {
                                                        itemCol.Item().PaddingLeft(15).PaddingTop(2)
                                                            .Text("— Không có task thuộc lịch trình này")
                                                            .FontSize(8).Italic().FontColor(Colors.Grey.Lighten1);
                                                    }
                                                });
                                            }
                                        });
                                    }
                                }
                            });
                        }

                        // ── Task chi tiết Section (Grouped by status) ──
                        if (!includeSchedules && includeTasks)
                        {
                            content.Item().PaddingBottom(16).Column(col =>
                            {
                                col.Item().Text("Danh sách nhiệm vụ (Task) chi tiết")
                                    .FontSize(13).Bold().FontColor(Colors.Blue.Darken2);

                                if (tasks.Count == 0)
                                {
                                    col.Item().PaddingTop(6).Text("Không có task phù hợp.").Italic().FontColor(Colors.Grey.Medium);
                                }
                                else
                                {
                                    var now = DateTime.Now;
                                    var overdueTasksList = tasks.Where(t => t.Status != TaskItemStatus.Completed && t.Deadline < now).ToList();
                                    var inProgressTasksList = tasks.Where(t => t.Status == TaskItemStatus.InProgress).ToList();
                                    var completedTasksList = tasks.Where(t => t.Status == TaskItemStatus.Completed).ToList();
                                    var pendingTasksList = tasks.Where(t => t.Status == TaskItemStatus.NotStarted && t.Deadline >= now).ToList();

                                    void DrawTaskGroup(string title, List<TaskItem> groupTasks, string titleColor, string headerBgColor)
                                    {
                                        if (groupTasks.Count == 0) return;
                                        
                                        col.Item().PaddingTop(10).Column(groupCol =>
                                        {
                                            groupCol.Item().Background(headerBgColor).Padding(5).Text($"{title} ({groupTasks.Count} mục)")
                                                .FontSize(11).Bold().FontColor(titleColor);

                                            groupCol.Item().PaddingTop(4).Table(table =>
                                            {
                                                table.ColumnsDefinition(cols =>
                                                {
                                                    cols.RelativeColumn(3); // Tiêu đề & Lịch trình cha
                                                    cols.RelativeColumn(1.5f); // Hạn chót
                                                    cols.RelativeColumn(1f); // Ưu tiên
                                                });

                                                table.Header(header =>
                                                {
                                                    header.Cell().Element(HeaderCell).Text("Nhiệm vụ & Lịch trình cha");
                                                    header.Cell().Element(HeaderCell).Text("Hạn chót");
                                                    header.Cell().Element(HeaderCell).Text("Mức độ");
                                                });

                                                foreach (var task in groupTasks)
                                                {
                                                    var parentScheduleText = task.ScheduleItem != null 
                                                        ? $"\nThuộc lịch trình: {task.ScheduleItem.Title} - {task.ScheduleItem.StartTime:dd/MM}"
                                                        : "\nKhông thuộc lịch trình";
                                                        
                                                    var priorityText = task.Priority switch
                                                    {
                                                        TaskPriorityLevel.High => "Cao",
                                                        TaskPriorityLevel.Medium => "TB",
                                                        TaskPriorityLevel.Low => "Thấp",
                                                        _ => "—"
                                                    };

                                                    table.Cell().Element(BodyCell).Text(text =>
                                                    {
                                                        text.Span(task.Title).Bold().FontSize(9);
                                                        text.Span(parentScheduleText).FontSize(8).Italic().FontColor(Colors.Grey.Medium);
                                                    });

                                                    table.Cell().Element(BodyCell).Text(task.Deadline.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                                                    table.Cell().Element(BodyCell).Text(priorityText).FontSize(9);
                                                }
                                            });
                                        });
                                    }

                                    DrawTaskGroup("Nhiệm vụ quá hạn", overdueTasksList, Colors.Red.Darken3, Colors.Red.Lighten5);
                                    DrawTaskGroup("Nhiệm vụ đang làm", inProgressTasksList, Colors.Blue.Darken3, Colors.Blue.Lighten5);
                                    DrawTaskGroup("Nhiệm vụ hoàn thành", completedTasksList, Colors.Green.Darken3, Colors.Green.Lighten5);
                                    DrawTaskGroup("Nhiệm vụ chưa xử lý", pendingTasksList, Colors.Orange.Darken3, Colors.Orange.Lighten5);
                                }
                            });
                        }

                        // ── Flat Schedules Section (only for personal summary) ──
                        if (includeSchedules && includeTasks && schedules.Count > 0)
                        {
                            content.Item().PaddingBottom(16).Column(col =>
                            {
                                col.Item().Text($"Danh sách lịch trình ({schedules.Count} mục)")
                                    .FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                                col.Item().PaddingTop(6).Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(3); // Tiêu đề
                                        cols.RelativeColumn(2); // Bắt đầu
                                        cols.RelativeColumn(2); // Kết thúc
                                        cols.RelativeColumn(2); // Địa điểm
                                        cols.RelativeColumn(1); // Quan trọng
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(HeaderCell).Text("Tiêu đề");
                                        header.Cell().Element(HeaderCell).Text("Bắt đầu");
                                        header.Cell().Element(HeaderCell).Text("Kết thúc");
                                        header.Cell().Element(HeaderCell).Text("Địa điểm");
                                        header.Cell().Element(HeaderCell).Text("Q.Trọng");
                                    });

                                    foreach (var item in schedules)
                                    {
                                        table.Cell().Element(BodyCell).Text(item.Title);
                                        table.Cell().Element(BodyCell).Text(item.StartTime.ToString("dd/MM/yyyy HH:mm"));
                                        table.Cell().Element(BodyCell).Text(item.EndTime.ToString("dd/MM/yyyy HH:mm"));
                                        table.Cell().Element(BodyCell).Text(item.Location ?? "—");
                                        table.Cell().Element(item.IsImportant ? ImportantCell : BodyCell)
                                            .Text(item.IsImportant ? "✓ Có" : "Không");
                                    }
                                });
                            });
                        }

                        // ── Flat Tasks Section (only for personal summary) ──
                        if (includeSchedules && includeTasks && tasks.Count > 0)
                        {
                            content.Item().PaddingBottom(8).Column(col =>
                            {
                                col.Item().Text($"Danh sách nhiệm vụ (Task) – {tasks.Count} mục")
                                    .FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                                col.Item().PaddingTop(6).Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(3); // Tiêu đề
                                        cols.RelativeColumn(2); // Hạn chót
                                        cols.RelativeColumn(2); // Trạng thái
                                        cols.RelativeColumn(1); // Ưu tiên
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(HeaderCell).Text("Tiêu đề task");
                                        header.Cell().Element(HeaderCell).Text("Hạn chót");
                                        header.Cell().Element(HeaderCell).Text("Trạng thái");
                                        header.Cell().Element(HeaderCell).Text("Ưu tiên");
                                    });

                                    var now = DateTime.Now;
                                    foreach (var task in tasks)
                                    {
                                        var isOverdue = task.Status != TaskItemStatus.Completed && task.Deadline < now;
                                        var statusText = task.Status switch
                                        {
                                            TaskItemStatus.Completed => "Hoàn thành",
                                            TaskItemStatus.InProgress => "Đang làm",
                                            TaskItemStatus.NotStarted when isOverdue => "Quá hạn",
                                            _ => "Chưa làm"
                                        };
                                        var priorityText = task.Priority switch
                                        {
                                            TaskPriorityLevel.High => "Cao",
                                            TaskPriorityLevel.Medium => "TB",
                                            TaskPriorityLevel.Low => "Thấp",
                                            _ => "—"
                                        };

                                        table.Cell().Element(BodyCell).Text(task.Title);
                                        table.Cell().Element(BodyCell).Text(task.Deadline.ToString("dd/MM/yyyy HH:mm"));
                                        table.Cell().Element(isOverdue ? OverdueCell : (task.Status == TaskItemStatus.Completed ? DoneCell : BodyCell))
                                            .Text(statusText);
                                        table.Cell().Element(BodyCell).Text(priorityText);
                                    }
                                });
                            });
                        }

                        // ── Performance Evaluation Comment ──
                        if (includeActivity && activitySummary.HasValue)
                        {
                            var s = activitySummary.Value;
                            var rate = s.Total == 0 ? 0 : (int)Math.Round(s.Completed * 100.0 / s.Total);
                            
                            string rating;
                            string comment;
                            string boxColor;
                            
                            if (rate >= 85)
                            {
                                rating = "Xuất sắc";
                                comment = "Hiệu suất làm việc tuyệt vời! Hoàn thành rất tốt các công việc đề ra đúng hạn.";
                                boxColor = Colors.Green.Lighten5;
                            }
                            else if (rate >= 50)
                            {
                                rating = "Khá tốt";
                                comment = "Hiệu suất khá tốt. Cần tập trung hoàn thành nốt các nhiệm vụ còn dang dở và tránh để quá hạn.";
                                boxColor = Colors.Blue.Lighten5;
                            }
                            else
                            {
                                rating = "Cần cải thiện";
                                comment = "Cảnh báo: Tỷ lệ hoàn thành công việc thấp. Cần rà soát ngay và xử lý dứt điểm các task quá hạn để không ảnh hưởng tiến độ.";
                                boxColor = Colors.Red.Lighten5;
                            }
                            
                            content.Item().PaddingTop(12).Background(boxColor).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(inner =>
                            {
                                inner.Item().Text($"Đánh giá hiệu suất cá nhân: {rating} ({rate}%)").FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                                inner.Item().PaddingTop(4).Text(comment).FontSize(10).Italic().FontColor(Colors.Grey.Darken2);
                            });
                        }
                    });

                    // ── Footer ──
                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text($"Schedule Manager – {ownerEmail}").FontSize(8).FontColor(Colors.Grey.Medium);
                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.Span("Trang ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.CurrentPageNumber().FontSize(8);
                            text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.TotalPages().FontSize(8);
                        });
                    });
                });
            }).GeneratePdf();
        }

        public static byte[] GenerateSystemOverview(SystemOverviewStats stats, DateTime? from, DateTime? to)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(text => text.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(inner =>
                            {
                                inner.Item().Text("Schedule Manager – Báo cáo tổng quan hệ thống")
                                    .FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                                inner.Item().Text("Báo cáo dành cho Quản trị viên (Admin)").FontSize(11).FontColor(Colors.Grey.Darken1);
                                var dateRange = (from, to) switch
                                {
                                    (null, null) => "Tất cả thời gian",
                                    (var f, null) => $"Từ {f:dd/MM/yyyy}",
                                    (null, var t) => $"Đến {t:dd/MM/yyyy}",
                                    (var f, var t) => $"Từ {f:dd/MM/yyyy} đến {t:dd/MM/yyyy}"
                                };
                                inner.Item().Text($"Thời gian dữ liệu: {dateRange}").FontSize(10).FontColor(Colors.Grey.Medium);
                                inner.Item().Text($"Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Lighten1);
                            });
                        });
                        col.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(Colors.Blue.Lighten3);
                        col.Item().PaddingBottom(4);
                    });

                    page.Content().Column(content =>
                    {
                        // 1. Thống kê Tài khoản Người dùng
                        content.Item().PaddingBottom(16).Column(col =>
                        {
                            col.Item().Text("Thống kê tài khoản người dùng").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                            col.Item().PaddingTop(6).Row(row =>
                            {
                                void StatBox(string label, int value, string color)
                                {
                                    row.RelativeItem().Padding(4).Background(color).Padding(10).Column(inner =>
                                    {
                                        inner.Item().Text(label).FontSize(9).FontColor(Colors.White);
                                        inner.Item().Text(value.ToString()).FontSize(18).Bold().FontColor(Colors.White);
                                    });
                                }
                                StatBox("Tổng User", stats.TotalUsers, Colors.Blue.Darken2);
                                StatBox("Đang hoạt động", stats.ActiveUsers, Colors.Green.Darken2);
                                StatBox("Bị khóa", stats.LockedUsers, Colors.Red.Darken2);
                                StatBox("Quản trị viên", stats.AdminUsers, Colors.Purple.Darken2);
                            });
                        });

                        // 2. Thống kê Lịch trình & Task
                        content.Item().PaddingBottom(16).Column(col =>
                        {
                            col.Item().Text("Thống kê Lịch trình & Task").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                            col.Item().PaddingTop(6).Row(row =>
                            {
                                void StatBox(string label, int value, string color)
                                {
                                    row.RelativeItem().Padding(4).Background(color).Padding(10).Column(inner =>
                                    {
                                        inner.Item().Text(label).FontSize(9).FontColor(Colors.White);
                                        inner.Item().Text(value.ToString()).FontSize(18).Bold().FontColor(Colors.White);
                                    });
                                }
                                StatBox("Tổng Lịch trình", stats.TotalSchedules, Colors.Blue.Darken1);
                                StatBox("Tổng Task", stats.TotalTasks, Colors.Indigo.Darken1);
                                StatBox("Hoàn thành", stats.CompletedTasks, Colors.Green.Darken1);
                                StatBox("Quá hạn", stats.OverdueTasks, Colors.Red.Darken1);
                            });
                        });

                        // Chi tiết chỉ số hệ thống
                        content.Item().PaddingBottom(12).Column(col =>
                        {
                            col.Item().Text("Các chỉ số hệ thống khác").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                            col.Item().PaddingTop(6).Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(3); // Chỉ số
                                    cols.RelativeColumn(1); // Giá trị
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderCell).Text("Chỉ số hệ thống");
                                    header.Cell().Element(HeaderCell).Text("Giá trị");
                                });

                                table.Cell().Element(BodyCell).Text("Số tài khoản User thông thường");
                                table.Cell().Element(BodyCell).Text(stats.RegularUsers.ToString());

                                table.Cell().Element(BodyCell).Text("Số hồ sơ công khai (Public Profiles)");
                                table.Cell().Element(BodyCell).Text(stats.PublicProfiles.ToString());

                                table.Cell().Element(BodyCell).Text("Phản ánh/báo cáo người dùng chờ xử lý (User Reports)");
                                table.Cell().Element(BodyCell).Text(stats.PendingReports.ToString());

                                var completionRate = stats.TotalTasks == 0 ? 0 : Math.Round(stats.CompletedTasks * 100.0 / stats.TotalTasks);
                                table.Cell().Element(BodyCell).Text("Tỷ lệ hoàn thành Task toàn hệ thống");
                                table.Cell().Element(BodyCell).Text($"{completionRate}%");
                            });
                        });
                        
                        // Nhận xét hệ thống
                        content.Item().PaddingTop(10).Background(Colors.Grey.Lighten4).Padding(10).Column(inner =>
                        {
                            inner.Item().Text("Nhận xét từ hệ thống:").FontSize(10).Bold().FontColor(Colors.Grey.Darken3);
                            string systemComment = "Hệ thống đang vận hành ổn định. ";
                            if (stats.PendingReports > 0)
                            {
                                systemComment += $"Lưu ý: Có {stats.PendingReports} báo cáo phản ánh từ người dùng đang chờ xử lý.";
                            }
                            else
                            {
                                systemComment += "Không có phản ánh nào chưa xử lý.";
                            }
                            inner.Item().Text(systemComment).FontSize(10).Italic().FontColor(Colors.Grey.Darken2);
                        });
                    });

                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text("Schedule Manager – Admin Report Center").FontSize(8).FontColor(Colors.Grey.Medium);
                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.Span("Trang ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.CurrentPageNumber().FontSize(8);
                            text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.TotalPages().FontSize(8);
                        });
                    });
                });
            }).GeneratePdf();
        }

        public static byte[] GenerateUsersReport(List<UserReportRow> rows, DateTime? from, DateTime? to)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(text => text.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(inner =>
                            {
                                inner.Item().Text("Schedule Manager – Báo cáo danh sách người dùng")
                                    .FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                                inner.Item().Text("Báo cáo hiệu suất hoạt động người dùng").FontSize(11).FontColor(Colors.Grey.Darken1);
                                var dateRange = (from, to) switch
                                {
                                    (null, null) => "Tất cả thời gian",
                                    (var f, null) => $"Từ {f:dd/MM/yyyy}",
                                    (null, var t) => $"Đến {t:dd/MM/yyyy}",
                                    (var f, var t) => $"Từ {f:dd/MM/yyyy} đến {t:dd/MM/yyyy}"
                                };
                                inner.Item().Text($"Thời gian dữ liệu: {dateRange}").FontSize(10).FontColor(Colors.Grey.Medium);
                                inner.Item().Text($"Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Lighten1);
                            });
                        });
                        col.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(Colors.Blue.Lighten3);
                        col.Item().PaddingBottom(4);
                    });

                    page.Content().Column(content =>
                    {
                        content.Item().PaddingBottom(12).Column(col =>
                        {
                            col.Item().Text($"Danh sách người dùng ({rows.Count} tài khoản)").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                            col.Item().PaddingTop(6).Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(3); // Email
                                    cols.RelativeColumn(2); // Vai trò
                                    cols.RelativeColumn(2); // Trạng thái
                                    cols.RelativeColumn(1.5f); // Số lịch
                                    cols.RelativeColumn(1.5f); // Số task
                                    cols.RelativeColumn(1.5f); // Đã xong
                                    cols.RelativeColumn(1.5f); // Quá hạn
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderCell).Text("Email / Tên hiển thị");
                                    header.Cell().Element(HeaderCell).Text("Vai trò");
                                    header.Cell().Element(HeaderCell).Text("Trạng thái");
                                    header.Cell().Element(HeaderCell).Text("Lịch trình");
                                    header.Cell().Element(HeaderCell).Text("Task");
                                    header.Cell().Element(HeaderCell).Text("Đã xong");
                                    header.Cell().Element(HeaderCell).Text("Quá hạn");
                                });

                                foreach (var r in rows)
                                {
                                    var userLabel = string.IsNullOrWhiteSpace(r.DisplayName) 
                                        ? r.Email 
                                        : $"{r.DisplayName}\n({r.Email})";
                                        
                                    table.Cell().Element(BodyCell).Text(userLabel).FontSize(8);
                                    table.Cell().Element(BodyCell).Text(r.Roles).FontSize(9);
                                    table.Cell().Element(r.IsLocked ? OverdueCell : DoneCell)
                                        .Text(r.IsLocked ? "Bị khóa" : "Hoạt động").FontSize(9);
                                    table.Cell().Element(BodyCell).Text(r.ScheduleCount.ToString()).FontSize(9);
                                    table.Cell().Element(BodyCell).Text(r.TaskCount.ToString()).FontSize(9);
                                    table.Cell().Element(BodyCell).Text(r.CompletedTaskCount.ToString()).FontSize(9);
                                    table.Cell().Element(r.OverdueTaskCount > 0 ? OverdueCell : BodyCell)
                                        .Text(r.OverdueTaskCount.ToString()).FontSize(9);
                                }
                            });
                        });
                    });

                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text("Schedule Manager – Admin Report Center").FontSize(8).FontColor(Colors.Grey.Medium);
                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.Span("Trang ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.CurrentPageNumber().FontSize(8);
                            text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.TotalPages().FontSize(8);
                        });
                    });
                });
            }).GeneratePdf();
        }

        private static IContainer HeaderCell(IContainer container) =>
            container.Background(Colors.Blue.Lighten3).Border(1).BorderColor(Colors.Blue.Lighten2).Padding(5)
                .DefaultTextStyle(t => t.Bold().FontSize(9));

        private static IContainer BodyCell(IContainer container) =>
            container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5);

        private static IContainer ImportantCell(IContainer container) =>
            container.BorderBottom(1).BorderColor(Colors.Orange.Lighten3).Padding(5)
                .DefaultTextStyle(t => t.FontColor(Colors.Orange.Darken3).Bold());

        private static IContainer OverdueCell(IContainer container) =>
            container.BorderBottom(1).BorderColor(Colors.Red.Lighten3).Padding(5)
                .DefaultTextStyle(t => t.FontColor(Colors.Red.Darken2));

        private static IContainer DoneCell(IContainer container) =>
            container.BorderBottom(1).BorderColor(Colors.Green.Lighten3).Padding(5)
                .DefaultTextStyle(t => t.FontColor(Colors.Green.Darken2));

        private static byte[] GenerateDoughnutChartImage(int completed, int inProgress, int overdue, int pending)
        {
            const int width = 300;
            const int height = 200;
            
            var info = new SKImageInfo(width, height);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            
            canvas.Clear(SKColors.Transparent);
            var total = completed + inProgress + overdue + pending;
            if (total == 0)
            {
                using var paintEmpty = new SKPaint
                {
                    Color = SKColors.LightGray,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 14,
                    IsAntialias = true
                };
                canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 3f, paintEmpty);
                
                using var font = new SKFont(SKTypeface.Default, 14);
                using var textPaint = new SKPaint
                {
                    Color = SKColors.Gray,
                    IsAntialias = true
                };
                canvas.DrawText("Không có dữ liệu", width / 2f, height / 2f + 5f, SKTextAlign.Center, font, textPaint);
                
                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                return data.ToArray();
            }

            float startAngle = 0;
            var segments = new (int count, SKColor color)[]
            {
                (completed, SKColor.Parse("#10b981")),
                (inProgress, SKColor.Parse("#3b82f6")),
                (overdue, SKColor.Parse("#ef4444")),
                (pending, SKColor.Parse("#f59e0b"))
            };

            var minDim = Math.Min(width, height);
            var rect = new SKRect(
                width / 2f - minDim / 3f,
                height / 2f - minDim / 3f,
                width / 2f + minDim / 3f,
                height / 2f + minDim / 3f
            );

            using var paintArc = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 16,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Butt
            };

            foreach (var seg in segments)
            {
                if (seg.count == 0) continue;
                var sweepAngle = (float)seg.count / total * 360f;
                paintArc.Color = seg.color;
                
                using var path = new SKPath();
                path.AddArc(rect, startAngle, sweepAngle);
                canvas.DrawPath(path, paintArc);

                startAngle += sweepAngle;
            }

            // Draw center text
            using var fontTitle = new SKFont(SKTypeface.Default, 9);
            using var paintTextTitle = new SKPaint
            {
                Color = SKColors.DarkGray,
                IsAntialias = true
            };
            canvas.DrawText("TỔNG SỐ TASK", width / 2f, height / 2f - 4f, SKTextAlign.Center, fontTitle, paintTextTitle);

            using var fontVal = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 18);
            using var paintTextVal = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true
            };
            canvas.DrawText(total.ToString(), width / 2f, height / 2f + 14f, SKTextAlign.Center, fontVal, paintTextVal);

            using var imageSnap = surface.Snapshot();
            using var pngData = imageSnap.Encode(SKEncodedImageFormat.Png, 100);
            return pngData.ToArray();
        }

        private static byte[] GenerateDailyTrendChartImage(List<TaskItem> tasks, DateTime? from, DateTime? to)
        {
            const int width = 450;
            const int height = 200;
            
            var info = new SKImageInfo(width, height);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            canvas.Clear(SKColors.Transparent);
            
            var startDate = from ?? (tasks.Count > 0 ? tasks.Min(t => t.Deadline).Date : DateTime.Today.AddDays(-6).Date);
            var endDate = to ?? (tasks.Count > 0 ? tasks.Max(t => t.Deadline).Date : DateTime.Today.Date);
            
            var totalDays = (endDate - startDate).Days + 1;
            if (totalDays <= 0) totalDays = 1;
            if (totalDays > 31)
            {
                startDate = endDate.AddDays(-30);
                totalDays = 31;
            }

            var dailyData = new (DateTime Date, int Completed, int Total)[totalDays];
            for (int i = 0; i < totalDays; i++)
            {
                var curDate = startDate.AddDays(i);
                var dayTasks = tasks.Where(t => t.Deadline.Date == curDate).ToList();
                var compCount = dayTasks.Count(t => t.Status == TaskItemStatus.Completed);
                dailyData[i] = (curDate, compCount, dayTasks.Count);
            }

            var maxTasksVal = dailyData.Max(d => d.Total);
            if (maxTasksVal == 0) maxTasksVal = 5;
            
            float margin = 20;
            float chartWidth = width - 2 * margin;
            float chartHeight = height - 2 * margin;

            using var paintGrid = new SKPaint
            {
                Color = SKColor.Parse("#e2e8f0"),
                StrokeWidth = 1,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            for (int i = 0; i <= 3; i++)
            {
                var y = margin + chartHeight * (3 - i) / 3f;
                canvas.DrawLine(margin, y, margin + chartWidth, y, paintGrid);
            }

            float colWidth = chartWidth / totalDays;
            
            using var paintBarTotal = new SKPaint
            {
                Color = SKColor.Parse("#dbeafe"),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            
            using var paintBarCompleted = new SKPaint
            {
                Color = SKColor.Parse("#3b82f6"),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            using var fontText = new SKFont(SKTypeface.Default, 10);
            using var paintText = new SKPaint
            {
                Color = SKColors.Gray,
                IsAntialias = true
            };

            for (int i = 0; i < totalDays; i++)
            {
                var d = dailyData[i];
                float x = margin + i * colWidth + colWidth * 0.1f;
                float w = colWidth * 0.8f;
                
                float totalHeight = (float)d.Total / maxTasksVal * chartHeight;
                float completedHeight = (float)d.Completed / maxTasksVal * chartHeight;

                var rectTotal = new SKRect(x, margin + chartHeight - totalHeight, x + w, margin + chartHeight);
                canvas.DrawRect(rectTotal, paintBarTotal);

                var rectCompleted = new SKRect(x, margin + chartHeight - completedHeight, x + w, margin + chartHeight);
                canvas.DrawRect(rectCompleted, paintBarCompleted);

                if (totalDays <= 10 || i % (totalDays / 5) == 0 || i == totalDays - 1)
                {
                    canvas.DrawText(d.Date.ToString("dd/MM"), x + w / 2f, margin + chartHeight + 12f, SKTextAlign.Center, fontText, paintText);
                }
            }

            using var paintAxis = new SKPaint
            {
                Color = SKColors.Gray,
                StrokeWidth = 1,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };
            canvas.DrawLine(margin, margin + chartHeight, margin + chartWidth, margin + chartHeight, paintAxis);

            using var imageSnap = surface.Snapshot();
            using var pngData = imageSnap.Encode(SKEncodedImageFormat.Png, 100);
            return pngData.ToArray();
        }
    }
}
