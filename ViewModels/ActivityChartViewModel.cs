namespace schedule.ViewModels
{
    public class ActivityChartViewModel
    {
        public string Title { get; set; } = string.Empty;
        public List<string> Labels { get; set; } = new();
        public List<int> Values { get; set; } = new();
        public string Color { get; set; } = "#0d6efd";
    }
}
