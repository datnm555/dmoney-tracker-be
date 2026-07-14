namespace Domain.Categories;

/// <summary>
/// The built-in categories seeded into the categories table for every user
/// (existing users get them via the AddCategoryCodeAndSeed migration; new
/// users at registration). Codes match Transactions.TransactionCategories.
/// </summary>
public static class SystemCategories
{
    public static readonly IReadOnlyList<(string Code, string Name, string Icon)> All =
    [
        ("living", "Sinh hoạt", "house"),
        ("salary", "Lương", "wallet"),
        ("education", "Tiền học", "graduation-cap"),
        ("food", "Ăn hàng", "utensils"),
        ("shopping", "Mua sắm", "shopping-bag"),
        ("bills", "Hóa đơn", "zap"),
        ("savings", "Tích luỹ", "piggy-bank"),
        ("other", "Khác", "tag")
    ];
}
