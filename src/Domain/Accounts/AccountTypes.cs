namespace Domain.Accounts;

/// <summary>
/// Account type codes. Comment-synced with the web repo (src/utils/accounts.ts).
/// <see cref="All"/> is also the display order: money types, then overdraft, then credit card.
/// </summary>
public static class AccountTypes
{
    public const string Checking = "checking";
    public const string Overdraft = "overdraft";
    public const string CreditCard = "credit_card";
    public const string Ewallet = "ewallet";
    public const string Cash = "cash";

    public static readonly IReadOnlyList<string> All = [Checking, Ewallet, Cash, Overdraft, CreditCard];

    public static bool IsValid(string type) => All.Contains(type, StringComparer.Ordinal);
}
