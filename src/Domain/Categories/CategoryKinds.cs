namespace Domain.Categories;

/// <summary>
/// Which transaction direction a category applies to: expense-only,
/// income-only, or both (e.g. "Khác", "Tích luỹ").
/// </summary>
public static class CategoryKinds
{
    public const string Expense = "expense";
    public const string Income = "income";
    public const string Both = "both";

    public const int MaxLength = 10;

    public static readonly IReadOnlyList<string> All = [Expense, Income, Both];

    public static bool IsValid(string kind) =>
        All.Contains(kind, StringComparer.Ordinal);
}
