using fatortak.Dtos.Dashboard;

namespace fatortak.Services.DashboardService
{
    public interface IDashboardService
    {
        Task<DashboardResponseDto> GetDashboardDataAsync(string period, Guid? branchId = null, Guid? projectId = null);
    }
}
