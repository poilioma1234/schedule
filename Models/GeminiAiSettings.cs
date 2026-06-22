namespace schedule.Models
{
    public class GeminiAiSettings
    {
        public string ApiKey { get; set; } = string.Empty;

        public string Model { get; set; } = "gemini-3.1-flash-lite";

        public string Endpoint { get; set; } = string.Empty;

        public int MaxOutputTokens { get; set; } = 1400;

        public double Temperature { get; set; } = 0.35;
    }
}
