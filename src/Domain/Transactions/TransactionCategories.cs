namespace Domain.Transactions;

public static class TransactionCategories
{
    public const string Other = "other";

    // Fits both built-in codes and a custom category's Guid rendered as a string (36 chars).
    public const int MaxLength = 36;

    // "transport" and "entertainment" are legacy-only: still valid so existing rows
    // keep working, but the frontend no longer offers them for new transactions.
    public static readonly IReadOnlyList<string> All =
    [
        "living", "salary", "education", "food", "shopping", "bills", "savings", "transport", "entertainment", Other
    ];

    public static bool IsValid(string category) =>
        All.Contains(category, StringComparer.Ordinal);

    /// <summary>
    /// A built-in code, or the Id of a user-defined category (its existence and
    /// ownership are verified by the handlers, which can reach the database).
    /// </summary>
    public static bool IsValidOrCustom(string category) =>
        IsValid(category) || Guid.TryParse(category, out _);

    /// <summary>The custom-category Id when the code is user-defined, otherwise null.</summary>
    public static Guid? CustomId(string? category) =>
        category is not null && Guid.TryParse(category, out Guid id) ? id : null;
}
