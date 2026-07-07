using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Transactions;
using Domain.Transactions;
using MockQueryable.NSubstitute;
using NSubstitute;
using Shouldly;

namespace Application.UnitTests.Transactions;

public class UpdateTransactionCommandHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    private IApplicationDbContext _dbContext = null!;
    private IUserContext _userContext = null!;

    private UpdateTransactionCommandHandler CreateHandler(params Transaction[] transactions)
    {
        _dbContext = Substitute.For<IApplicationDbContext>();
        _userContext = Substitute.For<IUserContext>();

        _userContext.UserId.Returns(UserId);
        var transactionsDbSet = transactions.ToList().BuildMockDbSet();
        _dbContext.Transactions.Returns(transactionsDbSet);

        return new UpdateTransactionCommandHandler(_dbContext, _userContext);
    }

    private static Transaction OwnTx() =>
        Transaction.Create(UserId, new DateOnly(2026, 7, 1), "cũ",
            Money.Create(1_000m).Value, Money.Zero(), null).Value;

    [Fact]
    public async Task Handle_WithOwnRecord_UpdatesAndSaves()
    {
        Transaction tx = OwnTx();
        var handler = CreateHandler(tx);
        var command = new UpdateTransactionCommand(
            tx.Id, new DateOnly(2026, 7, 10), "mới", 0m, 2_000m, "note", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        tx.Content.ShouldBe("mới");
        tx.Debit.Amount.ShouldBe(2_000m);
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithUnknownId_FailsNotFound()
    {
        var handler = CreateHandler(OwnTx());
        var command = new UpdateTransactionCommand(
            Guid.NewGuid(), new DateOnly(2026, 7, 10), "mới", 1m, 0m, null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.NotFound");
    }

    [Fact]
    public async Task Handle_WithOtherUsersRecord_FailsNotFound()
    {
        Transaction foreign = Transaction.Create(OtherUserId, new DateOnly(2026, 7, 1), "x",
            Money.Create(1m).Value, Money.Zero(), null).Value;
        var handler = CreateHandler(foreign);
        var command = new UpdateTransactionCommand(
            foreign.Id, new DateOnly(2026, 7, 10), "hack", 1m, 0m, null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.NotFound");
        await _dbContext.DidNotReceiveWithAnyArgs().SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WithInvalidData_FailsAndDoesNotSave()
    {
        Transaction tx = OwnTx();
        var handler = CreateHandler(tx);
        var command = new UpdateTransactionCommand(
            tx.Id, new DateOnly(2026, 7, 10), "mới", 0m, 0m, null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.EmptyAmount");
        await _dbContext.DidNotReceiveWithAnyArgs().SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WithCategory_UpdatesIt()
    {
        Transaction tx = OwnTx();
        var handler = CreateHandler(tx);
        var command = new UpdateTransactionCommand(
            tx.Id, new DateOnly(2026, 7, 10), "mới", 1m, 0m, null, "bills");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        tx.Category.ShouldBe("bills");
    }

    [Fact]
    public async Task Handle_PassesPaymentFieldsToTransaction()
    {
        Transaction tx = OwnTx();
        var handler = CreateHandler(tx);
        var command = new UpdateTransactionCommand(
            tx.Id, new DateOnly(2026, 7, 10), "Netflix", 0m, 260_000m, null,
            "entertainment", "card", "visa", "Techcombank");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        tx.PaymentMethod.ShouldBe(PaymentMethods.Card);
        tx.CardType.ShouldBe(CardTypes.Visa);
        tx.Bank.ShouldBe("Techcombank");
    }
}
