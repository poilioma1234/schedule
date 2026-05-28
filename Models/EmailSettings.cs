namespace schedule.Models
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = "Schedule Manager";
        public string SenderPassword { get; set; } = string.Empty;
        public bool EnableEmail { get; set; }
    }
}
