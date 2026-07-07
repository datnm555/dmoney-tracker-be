namespace Domain.Transactions;

public static class PaymentMethods
{
    public const string Transfer = "transfer";
    public const string Cash = "cash";
    public const string Card = "card";

    public const int MaxLength = 20;

    public static readonly IReadOnlyList<string> All = [Transfer, Cash, Card];

    public static bool IsValid(string paymentMethod) =>
        All.Contains(paymentMethod, StringComparer.Ordinal);
}
