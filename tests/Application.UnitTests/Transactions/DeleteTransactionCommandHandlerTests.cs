using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Transactions;
using Domain.Transactions;
using MockQueryable.NSubstitute;
using NSubstitute;
using Shouldly;

namespace Application.UnitTests.Transactions;

public class DeleteTransactionCommandHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    private IApplicationDbContext _dbContext = null!;
    private IUserContext _userContext = null!;

    private DeleteTransactionCommandHandler CreateHandler(params Transaction[] transactions)
    {
        _dbContext = Substitute.For<IApplicationDbContext>();
        _userContext = Substitute.For<IUserContext>();

        _userContext.UserId.Returns(UserId);
        var transactionsDbSet = transactions.ToList().BuildMockDbSet();
        _dbContext.Transactions.Returns(transactionsDbSet);

        return new DeleteTransactionCommandHandler(_dbContext, _userContext);
    }

    [Fact]
    public async Task Handle_WithOwnRecord_RemovesAndSaves()
    {
        Transaction tx = Transaction.Create(UserId, new DateOnly(2026, 7, 1), "x",
            Money.Create(1m).Value, Money.Zero(), null).Value;
        var handler = CreateHandler(tx);

        var result = await handler.Handle(new DeleteTransactionCommand(tx.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _dbContext.Transactions.Received(1).Remove(tx);
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithOtherUsersRecord_FailsNotFound()
    {
        Transaction foreign = Transaction.Create(OtherUserId, new DateOnly(2026, 7, 1), "x",
            Money.Create(1m).Value, Money.Zero(), null).Value;
        var handler = CreateHandler(foreign);

        var result = await handler.Handle(new DeleteTransactionCommand(foreign.Id), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.NotFound");
        await _dbContext.DidNotReceiveWithAnyArgs().SaveChangesAsync();
    }
}
