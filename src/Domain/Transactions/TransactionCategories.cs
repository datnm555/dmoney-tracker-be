namespace Domain.Transactions;

public static class TransactionCategories
{
    public const string Other = "other";

    public const int MaxLength = 30;

    public static readonly IReadOnlyList<string> All =
    [
        "food", "transport", "bills", "shopping", "entertainment", "salary", "education", Other
    ];

    public static bool IsValid(string category) =>
        All.Contains(category, StringComparer.Ordinal);
}
