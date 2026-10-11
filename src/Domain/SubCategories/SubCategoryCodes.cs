namespace Domain.SubCategories;

/// <summary>
/// Built-in codes for system sub-categories (seeded by migrations). Cross-repo contract:
/// the web repo matches these literally (SubCategoryResponse.code).
/// </summary>
public static class SubCategoryCodes
{
    /// <summary>"Sổ tiết kiệm" under the savings category — what the Savings page sums.</summary>
    public const string SavingsBook = "savings_book";
}
