using SharedKernel;

namespace Domain.Transactions;

public sealed class Money : ValueObject
{
    public const string DefaultCurrency = "VND";

    private Money() { }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = DefaultCurrency;

    /// <summary>
    /// Factory, not a shared static instance: EF Core owned types require a distinct
    /// object instance per owner, so a cached Zero would fail change tracking.
    /// </summary>
    public static Money Zero() => new(0m, DefaultCurrency);

    public static Result<Money> Create(decimal amount, string currency = DefaultCurrency)
    {
        if (amount < 0m)
        {
            return Result.Failure<Money>(MoneyErrors.NegativeAmount);
        }

        if (!string.Equals(currency, DefaultCurrency, StringComparison.Ordinal))
        {
            return Result.Failure<Money>(MoneyErrors.UnsupportedCurrency(currency));
        }

        return new Money(amount, currency);
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Amount;
        yield return Currency;
    }
}
