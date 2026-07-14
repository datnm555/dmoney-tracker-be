using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Transactions;
using Domain.Transactions;
using MockQueryable.NSubstitute;
using NSubstitute;
using SharedKernel;
using Shouldly;

namespace Application.UnitTests.Transactions;

public class GetDashboardStatsQueryHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    // Fixed "now" so the rolling window is deterministic: window = 2025-08 .. 2026-07.
    private static readonly DateTime Now = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

    private IApplicationDbContext _dbContext = null!;
    private IUserContext _userContext = null!;
    private IDateTimeProvider _clock = null!;

    private GetDashboardStatsQueryHandler CreateHandler(params Transaction[] transactions)
    {
        _dbContext = Substitute.For<IApplicationDbContext>();
        _userContext = Substitute.For<IUserContext>();
        _clock = Substitute.For<IDateTimeProvider>();

        _userContext.UserId.Returns(UserId);
        _clock.UtcNow.Returns(Now);
        var transactionsDbSet = transactions.ToList().BuildMockDbSet();
        _dbContext.Transactions.Returns(transactionsDbSet);
        return new GetDashboardStatsQueryHandler(_dbContext, _userContext, _clock);
    }

    private static Transaction Tx(
        Guid userId, DateOnly date, decimal credit, decimal debit, Guid? categoryId = null) =>
        Transaction.Create(
            userId, date, "tx", Money.Create(credit).Value, Money.Create(debit).Value, null, categoryId).Value;

    [Fact]
    public async Task Handle_Monthly_Returns12ZeroFilledMonths_OldestFirst()
    {
        var handler = CreateHandler(
            Tx(UserId, new DateOnly(2026, 7, 5), 15_000_000m, 0m),
            Tx(UserId, new DateOnly(2026, 3, 10), 0m, 2_000_000m),
            Tx(UserId, new DateOnly(2025, 7, 31), 999m, 0m)); // outside the window

        var result = await handler.Handle(new GetDashboardStatsQuery("2026-07"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Monthly.Count.ShouldBe(12);
        result.Value.Monthly[0].Month.ShouldBe("2025-08");
        result.Value.Monthly[11].Month.ShouldBe("2026-07");
        result.Value.Monthly[11].TotalCredit.Amount.ShouldBe(15_000_000m);
        result.Value.Monthly[7].Month.ShouldBe("2026-03");
        result.Value.Monthly[7].TotalDebit.Amount.ShouldBe(2_000_000m);
        result.Value.Monthly[7].Balance.Amount.ShouldBe(-2_000_000m);
        result.Value.Monthly[0].TotalCredit.Amount.ShouldBe(0m); // zero-filled
        result.Value.Monthly[0].Balance.Currency.ShouldBe("VND");
    }

    [Fact]
    public async Task Handle_Daily_OnlyDebitDaysOfSelectedMonth_SortedByDay()
    {
        var handler = CreateHandler(
            Tx(UserId, new DateOnly(2026, 7, 20), 0m, 100_000m),
            Tx(UserId, new DateOnly(2026, 7, 5), 0m, 250_000m),
            Tx(UserId, new DateOnly(2026, 7, 6), 1_000_000m, 0m),  // credit-only day: excluded
            Tx(UserId, new DateOnly(2026, 6, 5), 0m, 999m));        // other month: excluded

        var result = await handler.Handle(new GetDashboardStatsQuery("2026-07"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Daily.Count.ShouldBe(2);
        result.Value.Daily[0].Day.ShouldBe(5);
        result.Value.Daily[0].Debit.Amount.ShouldBe(250_000m);
        result.Value.Daily[1].Day.ShouldBe(20);
    }

    [Fact]
    public async Task Handle_ByCategory_GroupsByCategoryId_SortsByAmountDesc()
    {
        var foodId = Guid.NewGuid();
        var salaryId = Guid.NewGuid();
        var handler = CreateHandler(
            Tx(UserId, new DateOnly(2026, 7, 1), 0m, 200_000m, foodId),
            Tx(UserId, new DateOnly(2026, 7, 5), 0m, 90_000m, foodId),
            Tx(UserId, new DateOnly(2026, 7, 2), 0m, 50_000m),               // uncategorised bucket
            Tx(UserId, new DateOnly(2026, 7, 4), 5_000_000m, 0m, salaryId)); // credit-only: excluded

        var result = await handler.Handle(new GetDashboardStatsQuery("2026-07"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ByCategory.Count.ShouldBe(2);
        result.Value.ByCategory[0].CategoryId.ShouldBe(foodId);
        result.Value.ByCategory[0].Debit.Amount.ShouldBe(290_000m);
        result.Value.ByCategory[1].CategoryId.ShouldBeNull();
        result.Value.ByCategory[1].Debit.Amount.ShouldBe(50_000m);
    }

    [Fact]
    public async Task Handle_ExcludesOtherUsersRecords()
    {
        var handler = CreateHandler(
            Tx(OtherUserId, new DateOnly(2026, 7, 5), 1_000_000m, 500_000m, Guid.NewGuid()));

        var result = await handler.Handle(new GetDashboardStatsQuery("2026-07"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Monthly[11].TotalCredit.Amount.ShouldBe(0m);
        result.Value.Daily.ShouldBeEmpty();
        result.Value.ByCategory.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("2026-13")]
    [InlineData("")]
    public async Task Handle_WithInvalidMonth_Fails(string month)
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetDashboardStatsQuery(month), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.InvalidMonth");
    }

    [Fact]
    public async Task Handle_WithoutUser_FailsUnauthenticated()
    {
        _dbContext = Substitute.For<IApplicationDbContext>();
        _userContext = Substitute.For<IUserContext>();
        _clock = Substitute.For<IDateTimeProvider>();

        _userContext.UserId.Returns((Guid?)null);
        _clock.UtcNow.Returns(Now);
        var transactionsDbSet = new List<Transaction>().BuildMockDbSet();
        _dbContext.Transactions.Returns(transactionsDbSet);
        var handler = new GetDashboardStatsQueryHandler(_dbContext, _userContext, _clock);

        var result = await handler.Handle(new GetDashboardStatsQuery("2026-07"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.Unauthenticated");
    }
}
