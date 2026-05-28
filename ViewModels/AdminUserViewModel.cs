namespace schedule.ViewModels
{
    public class AdminUserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Roles { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
    }
}
