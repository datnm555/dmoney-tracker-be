namespace Application.Transactions.Data;

public sealed record DashboardStatsResponse(
    IReadOnlyList<MonthlyStatResponse> Monthly,
    IReadOnlyList<DailyStatResponse> Daily,
    IReadOnlyList<CategoryStatResponse> ByCategory);
