using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Transactions;
using Domain.Transactions;
using MockQueryable.NSubstitute;
using NSubstitute;
using Shouldly;

namespace Application.UnitTests.Transactions;

public class GetOpenAdvancesQueryHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    private static GetOpenAdvancesQueryHandler CreateHandler(params Transaction[] transactions)
    {
        var dbContext = Substitute.For<IApplicationDbContext>();
        var userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        var transactionsDbSet = transactions.ToList().BuildMockDbSet();
        dbContext.Transactions.Returns(transactionsDbSet);
        return new GetOpenAdvancesQueryHandler(dbContext, userContext);
    }

    private static Transaction Advance(Guid userId, string content) =>
        Transaction.Create(
            userId, new DateOnly(2026, 7, 1), content,
            Money.Zero(), Money.Create(1_000_000m).Value, null,
            null, null, null, null, true).Value;

    private static Transaction Reimbursement(Guid advanceId) =>
        Transaction.Create(
            UserId, new DateOnly(2026, 7, 5), "Hoàn ứng",
            Money.Create(1_000_000m).Value, Money.Zero(), null,
            null, null, null, null, false, advanceId).Value;

    [Fact]
    public async Task Handle_ReturnsOnlyOwnOpenAdvances()
    {
        Transaction open = Advance(UserId, "Còn mở");
        Transaction settledAdvance = Advance(UserId, "Đã hoàn");
        Transaction reimbursement = Reimbursement(settledAdvance.Id);
        Transaction foreign = Advance(OtherUserId, "Của người khác");
        var handler = CreateHandler(open, settledAdvance, reimbursement, foreign);

        var result = await handler.Handle(new GetOpenAdvancesQuery(null), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        result.Value[0].Content.ShouldBe("Còn mở");
        result.Value[0].Debit.Amount.ShouldBe(1_000_000m);
    }

    [Fact]
    public async Task Handle_ForTransaction_KeepsItsOwnLinkedAdvance()
    {
        Transaction settledAdvance = Advance(UserId, "Đã hoàn bởi chính nó");
        Transaction reimbursement = Reimbursement(settledAdvance.Id);
        var handler = CreateHandler(settledAdvance, reimbursement);

        var result = await handler.Handle(
            new GetOpenAdvancesQuery(reimbursement.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        result.Value[0].Id.ShouldBe(settledAdvance.Id);
    }
}
