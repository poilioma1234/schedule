namespace schedule.ViewModels
{
    public class SearchViewModel
    {
        public string Query { get; set; } = string.Empty;
        public List<SearchResultViewModel> Results { get; set; } = new();
    }

    public class SearchResultViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}
