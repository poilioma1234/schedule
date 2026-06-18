using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using schedule.Models;

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
                                inner.Item().Text("Schedule Manager – Báo cáo tổng hợp")
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

                        // ── Schedules Section ──
                        if (includeSchedules && schedules.Count > 0)
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
                        else if (includeSchedules)
                        {
                            content.Item().PaddingBottom(12).Text("Lịch trình: Không có dữ liệu phù hợp.").FontColor(Colors.Grey.Medium).Italic();
                        }

                        // ── Tasks Section ──
                        if (includeTasks && tasks.Count > 0)
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
                        else if (includeTasks)
                        {
                            content.Item().PaddingBottom(8).Text("Nhiệm vụ: Không có dữ liệu phù hợp.").FontColor(Colors.Grey.Medium).Italic();
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
    }
}
