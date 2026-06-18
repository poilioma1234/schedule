using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace schedule.Helpers
{
    public static class AdminPdfGenerator
    {
        public static byte[] GenerateUserStats(
            List<(string Email, string Roles, bool IsLocked, int Schedules, int Tasks)> users,
            DateTime generatedAt)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(t => t.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Báo cáo Thống kê Người dùng")
                            .FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().Text($"Xuất lúc: {generatedAt:dd/MM/yyyy HH:mm} · Tổng: {users.Count} người dùng")
                            .FontSize(9).FontColor(Colors.Grey.Medium);
                        col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Colors.Blue.Lighten3);
                        col.Item().PaddingBottom(4);
                    });

                    page.Content().Column(content =>
                    {
                        // Summary row
                        content.Item().PaddingBottom(12).Row(row =>
                        {
                            void Box(string label, string value, string color) =>
                                row.RelativeItem().Padding(4).Background(color).Padding(10).Column(inner =>
                                {
                                    inner.Item().Text(label).FontSize(9).FontColor(Colors.White);
                                    inner.Item().Text(value).FontSize(18).Bold().FontColor(Colors.White);
                                });

                            Box("Tổng", users.Count.ToString(), Colors.Blue.Darken2);
                            Box("Hoạt động", users.Count(u => !u.IsLocked).ToString(), Colors.Green.Darken2);
                            Box("Bị khóa", users.Count(u => u.IsLocked).ToString(), Colors.Red.Darken2);
                            Box("Admin", users.Count(u => u.Roles.Contains("Admin")).ToString(), Colors.Orange.Darken2);
                        });

                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3);
                                cols.RelativeColumn(1.5f);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                                cols.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                IContainer HC(IContainer c) => c.Background(Colors.Blue.Lighten3).Border(1).BorderColor(Colors.Blue.Lighten2).Padding(5).DefaultTextStyle(t => t.Bold().FontSize(9));
                                header.Cell().Element(HC).Text("Email");
                                header.Cell().Element(HC).Text("Vai trò");
                                header.Cell().Element(HC).Text("Trạng thái");
                                header.Cell().Element(HC).Text("Lịch");
                                header.Cell().Element(HC).Text("Task");
                            });

                            IContainer BC(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5);
                            IContainer LC(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Red.Lighten3).Padding(5).DefaultTextStyle(t => t.FontColor(Colors.Red.Darken2));

                            foreach (var u in users)
                            {
                                table.Cell().Element(BC).Text(u.Email);
                                table.Cell().Element(BC).Text(u.Roles);
                                table.Cell().Element(u.IsLocked ? LC : BC).Text(u.IsLocked ? "Bị khóa" : "Hoạt động");
                                table.Cell().Element(BC).Text(u.Schedules.ToString());
                                table.Cell().Element(BC).Text(u.Tasks.ToString());
                            }
                        });
                    });

                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text("Schedule Manager – Admin Report").FontSize(8).FontColor(Colors.Grey.Medium);
                        row.RelativeItem().AlignRight().Text(t =>
                        {
                            t.Span("Trang ").FontSize(8);
                            t.CurrentPageNumber().FontSize(8);
                            t.Span(" / ").FontSize(8);
                            t.TotalPages().FontSize(8);
                        });
                    });
                });
            }).GeneratePdf();
        }
    }
}
