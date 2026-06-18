namespace schedule.Models
{
    public enum ReportStatus
    {
        Pending,      // Chờ xử lý
        Warned,       // Đã cảnh báo
        Locked,       // Đã khóa tài khoản
        Dismissed     // Bỏ qua
    }

    public class UserReport
    {
        public int Id { get; set; }

        /// <summary>UserId của người bị báo cáo</summary>
        public string ReportedUserId { get; set; } = string.Empty;

        /// <summary>UserId của người gửi báo cáo (null nếu anonymous)</summary>
        public string? ReporterUserId { get; set; }

        /// <summary>Lý do báo cáo do người dùng nhập</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>Danh mục báo cáo</summary>
        public string Category { get; set; } = "other";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ReportStatus Status { get; set; } = ReportStatus.Pending;

        /// <summary>Ghi chú xử lý của admin</summary>
        public string? AdminNote { get; set; }

        public DateTime? HandledAt { get; set; }
    }
}
