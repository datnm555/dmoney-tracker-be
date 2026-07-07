using Domain.Transactions;
using Shouldly;

namespace Application.UnitTests.Transactions;

public class MoneyTests
{
    [Fact]
    public void Create_WithPositiveAmount_DefaultsToVnd()
    {
        var result = Money.Create(15_000_000m);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Amount.ShouldBe(15_000_000m);
        result.Value.Currency.ShouldBe("VND");
    }

    [Fact]
    public void Create_WithZeroAmount_Succeeds()
    {
        Money.Create(0m).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithNegativeAmount_Fails()
    {
        var result = Money.Create(-1m);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Money.NegativeAmount");
    }

    [Fact]
    public void Create_WithUnsupportedCurrency_Fails()
    {
        var result = Money.Create(100m, "USD");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Money.UnsupportedCurrency");
    }

    [Fact]
    public void Zero_ReturnsDistinctInstances()
    {
        Money first = Money.Zero();
        Money second = Money.Zero();

        ReferenceEquals(first, second).ShouldBeFalse();
        first.ShouldBe(second);
    }
}
