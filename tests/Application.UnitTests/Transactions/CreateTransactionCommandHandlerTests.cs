using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Transactions;
using Domain.Transactions;
using MockQueryable.NSubstitute;
using NSubstitute;
using SharedKernel;
using Shouldly;

namespace Application.UnitTests.Transactions;

public class CreateTransactionCommandHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private IApplicationDbContext _dbContext = null!;
    private IUserContext _userContext = null!;

    private CreateTransactionCommandHandler CreateHandler(Guid? userId, params Transaction[] existingTransactions)
    {
        _dbContext = Substitute.For<IApplicationDbContext>();
        _userContext = Substitute.For<IUserContext>();

        _userContext.UserId.Returns(userId);
        var transactionsDbSet = existingTransactions.ToList().BuildMockDbSet();
        _dbContext.Transactions.Returns(transactionsDbSet);

        return new CreateTransactionCommandHandler(_dbContext, _userContext);
    }

    private static CreateTransactionCommand ValidCommand() =>
        new(new DateOnly(2026, 7, 6), "Lương tháng 7", 15_000_000m, 0m, null, null);

    [Fact]
    public async Task Handle_WithValidCommand_SavesForCurrentUser()
    {
        var handler = CreateHandler(UserId);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(Guid.Empty);
        _dbContext.Transactions.Received(1).Add(Arg.Is<Transaction>(t => t.UserId == UserId));
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutUser_FailsUnauthenticated()
    {
        var handler = CreateHandler(null);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.Unauthenticated");
        await _dbContext.DidNotReceiveWithAnyArgs().SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WithNegativeCredit_Fails()
    {
        var handler = CreateHandler(UserId);
        var command = new CreateTransactionCommand(new DateOnly(2026, 7, 6), "x", -1m, 0m, null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Money.NegativeAmount");
    }

    [Fact]
    public async Task Handle_WithBothAmountsZero_Fails()
    {
        var handler = CreateHandler(UserId);
        var command = new CreateTransactionCommand(new DateOnly(2026, 7, 6), "x", 0m, 0m, null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.EmptyAmount");
    }

    [Fact]
    public async Task Handle_WithCategory_PersistsIt()
    {
        var handler = CreateHandler(UserId);
        var command = new CreateTransactionCommand(new DateOnly(2026, 7, 6), "Ăn trưa", 0m, 50_000m, null, "food");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _dbContext.Transactions.Received(1).Add(Arg.Is<Transaction>(t => t.Category == "food"));
    }

    [Fact]
    public async Task Handle_PassesPaymentFieldsToTransaction()
    {
        var handler = CreateHandler(UserId);
        Transaction? captured = null;
        _dbContext.Transactions.When(x => x.Add(Arg.Any<Transaction>()))
            .Do(x => captured = x.Arg<Transaction>());

        var command = new CreateTransactionCommand(
            new DateOnly(2026, 7, 7), "Netflix", 0m, 260_000m, null,
            "entertainment", "card", "visa", "Techcombank");

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        captured.ShouldNotBeNull();
        captured.PaymentMethod.ShouldBe(PaymentMethods.Card);
        captured.CardType.ShouldBe(CardTypes.Visa);
        captured.Bank.ShouldBe("Techcombank");
    }

    [Fact]
    public async Task Handle_PersistsIsAdvanceFlag()
    {
        var handler = CreateHandler(UserId);
        Transaction? captured = null;
        _dbContext.Transactions.When(x => x.Add(Arg.Any<Transaction>()))
            .Do(x => captured = x.Arg<Transaction>());

        var command = new CreateTransactionCommand(
            new DateOnly(2026, 7, 9), "Tiền xe bus ứng trước", 0m, 2_000_000m, null,
            null, null, null, null, true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        captured.ShouldNotBeNull();
        captured.IsAdvance.ShouldBeTrue();
    }
}
