using SharedKernel;

namespace Domain.Transactions;

public static class MoneyErrors
{
    public static readonly Error NegativeAmount = Error.Validation(
        "Money.NegativeAmount",
        "Amount must not be negative.");

    public static Error UnsupportedCurrency(string currency) => Error.Validation(
        "Money.UnsupportedCurrency",
        $"Currency '{currency}' is not supported.");
}
