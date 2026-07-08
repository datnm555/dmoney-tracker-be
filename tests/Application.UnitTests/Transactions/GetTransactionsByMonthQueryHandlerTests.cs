using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Transactions;
using Domain.Transactions;
using MockQueryable.NSubstitute;
using NSubstitute;
using Shouldly;

namespace Application.UnitTests.Transactions;

public class GetTransactionsByMonthQueryHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    private IApplicationDbContext _dbContext = null!;
    private IUserContext _userContext = null!;

    private GetTransactionsByMonthQueryHandler CreateHandler(params Transaction[] transactions)
    {
        _dbContext = Substitute.For<IApplicationDbContext>();
        _userContext = Substitute.For<IUserContext>();

        _userContext.UserId.Returns(UserId);
        var transactionsDbSet = transactions.ToList().BuildMockDbSet();
        _dbContext.Transactions.Returns(transactionsDbSet);
        return new GetTransactionsByMonthQueryHandler(_dbContext, _userContext);
    }

    private static Transaction Tx(Guid userId, DateOnly date, decimal credit, decimal debit) =>
        Transaction.Create(
            userId, date, "tx", Money.Create(credit).Value, Money.Create(debit).Value, null).Value;

    [Fact]
    public async Task Handle_ReturnsOnlyCurrentUsersRecordsInMonth_WithTotals()
    {
        var handler = CreateHandler(
            Tx(UserId, new DateOnly(2026, 7, 1), 15_000_000m, 0m),
            Tx(UserId, new DateOnly(2026, 7, 15), 0m, 8_200_000m),
            Tx(UserId, new DateOnly(2026, 6, 30), 999m, 0m),          // other month
            Tx(OtherUserId, new DateOnly(2026, 7, 2), 555m, 0m));     // other user

        var result = await handler.Handle(new GetTransactionsByMonthQuery("2026-07"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
        result.Value.Items[0].Date.ShouldBe(new DateOnly(2026, 7, 15)); // newest first
        result.Value.TotalCredit.Amount.ShouldBe(15_000_000m);
        result.Value.TotalDebit.Amount.ShouldBe(8_200_000m);
        result.Value.Balance.Amount.ShouldBe(6_800_000m);
        result.Value.Balance.Currency.ShouldBe("VND");
    }

    [Fact]
    public async Task Handle_WithNoRecords_ReturnsEmptyItemsAndZeroTotals()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetTransactionsByMonthQuery("2026-07"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.TotalCredit.Amount.ShouldBe(0m);
        result.Value.Balance.Amount.ShouldBe(0m);
    }

    [Fact]
    public async Task Handle_BalanceCanBeNegative()
    {
        var handler = CreateHandler(Tx(UserId, new DateOnly(2026, 7, 3), 0m, 500_000m));

        var result = await handler.Handle(new GetTransactionsByMonthQuery("2026-07"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Balance.Amount.ShouldBe(-500_000m);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("07-2026")]
    [InlineData("garbage")]
    [InlineData("")]
    public async Task Handle_WithInvalidMonth_Fails(string month)
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetTransactionsByMonthQuery(month), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.InvalidMonth");
    }

    [Fact]
    public async Task Handle_ProjectsPaymentFieldsIntoResponse()
    {
        Transaction tx = Transaction.Create(
            UserId, new DateOnly(2026, 7, 5), "Netflix",
            Money.Zero(), Money.Create(260_000m).Value, null,
            "entertainment", PaymentMethods.Card, CardTypes.Visa, "Techcombank").Value;
        var handler = CreateHandler(tx);

        var result = await handler.Handle(new GetTransactionsByMonthQuery("2026-07"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items[0].PaymentMethod.ShouldBe(PaymentMethods.Card);
        result.Value.Items[0].CardType.ShouldBe(CardTypes.Visa);
        result.Value.Items[0].Bank.ShouldBe("Techcombank");
    }

    [Fact]
    public async Task Handle_WithYearOnly_ReturnsWholeYearWithTotals()
    {
        var handler = CreateHandler(
            Tx(UserId, new DateOnly(2026, 1, 5), 10_000_000m, 0m),
            Tx(UserId, new DateOnly(2026, 7, 15), 0m, 4_000_000m),
            Tx(UserId, new DateOnly(2025, 12, 31), 999m, 0m),         // previous year
            Tx(OtherUserId, new DateOnly(2026, 3, 1), 555m, 0m));    // other user

        var result = await handler.Handle(new GetTransactionsByMonthQuery("2026"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
        result.Value.TotalCredit.Amount.ShouldBe(10_000_000m);
        result.Value.TotalDebit.Amount.ShouldBe(4_000_000m);
        result.Value.Balance.Amount.ShouldBe(6_000_000m);
    }

    [Fact]
    public async Task Handle_WithMalformedYear_Fails()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetTransactionsByMonthQuery("202A"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransactionErrors.InvalidMonth);
    }
}
