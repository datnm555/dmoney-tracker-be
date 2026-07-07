namespace Domain.Transactions;

public static class CardTypes
{
    public const string Visa = "visa";
    public const string Credit = "credit";

    public const int MaxLength = 20;

    public static readonly IReadOnlyList<string> All = [Visa, Credit];

    public static bool IsValid(string cardType) =>
        All.Contains(cardType, StringComparer.Ordinal);
}
