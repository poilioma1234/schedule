using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using schedule.Models;

namespace schedule.Helpers
{
    public static class SchedulePdfGenerator
    {
        public static byte[] Generate(IEnumerable<ScheduleItem> items, string ownerEmail)
        {
            var rows = items.OrderBy(item => item.StartTime).ToList();

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(text => text.FontSize(11));

                    page.Header().Column(column =>
                    {
                        column.Item().Text("Schedule Manager").FontSize(22).Bold();
                        column.Item().Text($"Lịch trình của: {ownerEmail}");
                        column.Item().Text($"Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}");
                    });

                    page.Content().PaddingVertical(20).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Tiêu đề");
                            header.Cell().Element(HeaderCell).Text("Bắt đầu");
                            header.Cell().Element(HeaderCell).Text("Kết thúc");
                            header.Cell().Element(HeaderCell).Text("Địa điểm");
                            header.Cell().Element(HeaderCell).Text("Quan trọng");
                        });

                        foreach (var item in rows)
                        {
                            table.Cell().Element(BodyCell).Text(item.Title);
                            table.Cell().Element(BodyCell).Text(item.StartTime.ToString("dd/MM/yyyy HH:mm"));
                            table.Cell().Element(BodyCell).Text(item.EndTime.ToString("dd/MM/yyyy HH:mm"));
                            table.Cell().Element(BodyCell).Text(item.Location ?? "");
                            table.Cell().Element(BodyCell).Text(item.IsImportant ? "Có" : "Không");
                        }
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Trang ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            }).GeneratePdf();
        }

        private static IContainer HeaderCell(IContainer container)
        {
            return container
                .Background(Colors.Blue.Lighten4)
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(6)
                .DefaultTextStyle(text => text.Bold());
        }

        private static IContainer BodyCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3)
                .Padding(6);
        }
    }
}
