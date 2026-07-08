namespace Domain.Transactions;

public static class TransactionCategories
{
    public const string Other = "other";

    public const int MaxLength = 30;

    // "transport" and "entertainment" are legacy-only: still valid so existing rows
    // keep working, but the frontend no longer offers them for new transactions.
    public static readonly IReadOnlyList<string> All =
    [
        "living", "salary", "education", "food", "shopping", "bills", "savings", "transport", "entertainment", Other
    ];

    public static bool IsValid(string category) =>
        All.Contains(category, StringComparer.Ordinal);
}
