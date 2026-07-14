using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Transactions;
using Domain.Transactions;
using MockQueryable.NSubstitute;
using NSubstitute;
using Shouldly;

namespace Application.UnitTests.Transactions;

public class GetCreditsQueryHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ReturnsOwnPlainCreditsOnly_NewestFirst()
    {
        Transaction credit = Transaction.Create(UserId, new DateOnly(2026, 2, 1), "Hoàn tiền",
            Money.Create(10_000_000m).Value, Money.Zero(), null).Value;
        Transaction olderCredit = Transaction.Create(UserId, new DateOnly(2026, 1, 5), "Lương",
            Money.Create(15_000_000m).Value, Money.Zero(), null).Value;
        Transaction debit = Transaction.Create(UserId, new DateOnly(2026, 2, 2), "Chi",
            Money.Zero(), Money.Create(1_000m).Value, null).Value;
        Transaction foreign = Transaction.Create(Guid.NewGuid(), new DateOnly(2026, 2, 3), "Khác",
            Money.Create(9_000m).Value, Money.Zero(), null).Value;
        var dbContext = Substitute.For<IApplicationDbContext>();
        var userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        var dbSet = new List<Transaction> { credit, olderCredit, debit, foreign }.BuildMockDbSet();
        dbContext.Transactions.Returns(dbSet);
        var handler = new GetCreditsQueryHandler(dbContext, userContext);

        var result = await handler.Handle(new GetCreditsQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value[0].Content.ShouldBe("Hoàn tiền");
        result.Value[1].Content.ShouldBe("Lương");
    }
}
