namespace Domain.Transactions;

public static class CardTypes
{
    public const string Debit = "debit";
    public const string Credit = "credit";

    public const int MaxLength = 20;

    public static readonly IReadOnlyList<string> All = [Debit, Credit];

    public static bool IsValid(string cardType) =>
        All.Contains(cardType, StringComparer.Ordinal);
}
