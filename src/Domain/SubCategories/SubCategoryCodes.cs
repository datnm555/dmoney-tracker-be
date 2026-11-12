namespace Domain.SubCategories;

/// <summary>
/// Built-in codes for system sub-categories (seeded by migrations). Cross-repo contract:
/// the web repo matches these literally (SubCategoryResponse.code).
/// </summary>
public static class SubCategoryCodes
{
    /// <summary>"Sổ tiết kiệm" under the savings category — what the Savings page sums.</summary>
    public const string SavingsBook = "savings_book";

    /// <summary>
    /// "Chuyển giữa tài khoản" under "Chuyển tiền nội bộ" (excluded from stats): money moved between
    /// the owner's accounts, including paying a card whose swipes were already recorded.
    /// </summary>
    public const string AccountTransfer = "account_transfer";

    /// <summary>
    /// "Trả thẻ tín dụng" under "Nợ" (counted): paying a card statement whose spending was never
    /// recorded swipe by swipe, so the payment itself is the expense.
    /// </summary>
    public const string CardPayment = "card_payment";
}
