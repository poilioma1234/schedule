using schedule.Models;

namespace schedule.Services
{
    public interface ILeaderboardService
    {
        Task<List<LeaderboardEntry>> BuildRowsAsync(DateTime startDate, DateTime exclusiveEndDate);
        Task<List<LeaderboardAward>> EnsureMonthlyAwardsAsync(DateTime monthStart);
        Task EnsureAllHistoricalAwardsAsync();
    }
}
