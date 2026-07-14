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
    private static readonly Domain.Categories.Category Food =
        Domain.Categories.Category.Create("Ăn hàng", "utensils", "tester", "food").Value;

    private IApplicationDbContext _dbContext = null!;
    private IUserContext _userContext = null!;

    private CreateTransactionCommandHandler CreateHandler(Guid? userId, params Transaction[] existingTransactions)
    {
        _dbContext = Substitute.For<IApplicationDbContext>();
        _userContext = Substitute.For<IUserContext>();

        _userContext.UserId.Returns(userId);
        var transactionsDbSet = existingTransactions.ToList().BuildMockDbSet();
        _dbContext.Transactions.Returns(transactionsDbSet);
        var categoriesDbSet = new List<Domain.Categories.Category> { Food }.BuildMockDbSet();
        _dbContext.Categories.Returns(categoriesDbSet);

        return new CreateTransactionCommandHandler(_dbContext, _userContext);
    }

    private static CreateTransactionCommand ValidCommand() =>
        new(new DateOnly(2026, 7, 6), "Lương tháng 7", 15_000_000m, 0m, null, Food.Id);

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
        var command = new CreateTransactionCommand(new DateOnly(2026, 7, 6), "x", -1m, 0m, null, Food.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Money.NegativeAmount");
    }

    [Fact]
    public async Task Handle_WithBothAmountsZero_Fails()
    {
        var handler = CreateHandler(UserId);
        var command = new CreateTransactionCommand(new DateOnly(2026, 7, 6), "x", 0m, 0m, null, Food.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.EmptyAmount");
    }

    [Fact]
    public async Task Handle_WithCategory_PersistsIt()
    {
        var handler = CreateHandler(UserId);
        var command = new CreateTransactionCommand(new DateOnly(2026, 7, 6), "Ăn trưa", 0m, 50_000m, null, Food.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _dbContext.Transactions.Received(1).Add(Arg.Is<Transaction>(t => t.CategoryId == Food.Id));
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
            Food.Id, "card", "visa", "Techcombank");

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
            Food.Id, null, null, null, true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        captured.ShouldNotBeNull();
        captured.IsAdvance.ShouldBeTrue();
    }

    private static Transaction OpenAdvance(Guid userId, decimal amount = 2_000_000m) =>
        Transaction.Create(
            userId, new DateOnly(2026, 7, 1), "Ứng trước xe bus",
            Money.Zero(), Money.Create(amount).Value, null,
            null, null, null, null, true).Value;

    [Fact]
    public async Task Handle_LinksCreditToOpenAdvance()
    {
        Transaction advance = OpenAdvance(UserId);
        var handler = CreateHandler(UserId, advance);
        Transaction? captured = null;
        _dbContext.Transactions.When(x => x.Add(Arg.Any<Transaction>()))
            .Do(x => captured = x.Arg<Transaction>());

        var command = new CreateTransactionCommand(
            new DateOnly(2026, 7, 9), "Hoàn ứng", 2_000_000m, 0m, null,
            Food.Id, null, null, null, false, [advance.Id]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        captured.ShouldNotBeNull();
        advance.ReimbursedByTransactionId.ShouldBe(captured.Id);
    }

    [Fact]
    public async Task Handle_AdvanceNotFound_Fails()
    {
        var handler = CreateHandler(UserId);

        var command = new CreateTransactionCommand(
            new DateOnly(2026, 7, 9), "Hoàn ứng", 2_000_000m, 0m, null,
            Food.Id, null, null, null, false, [Guid.NewGuid()]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.AdvanceNotFound");
    }

    [Fact]
    public async Task Handle_AdvanceAlreadySettled_Fails()
    {
        Transaction advance = OpenAdvance(UserId);
        advance.MarkReimbursedBy(Guid.NewGuid());
        var handler = CreateHandler(UserId, advance);

        var command = new CreateTransactionCommand(
            new DateOnly(2026, 7, 9), "Hoàn ứng lần 2", 2_000_000m, 0m, null,
            Food.Id, null, null, null, false, [advance.Id]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.AdvanceAlreadySettled");
    }

    [Fact]
    public async Task Handle_LinksMultipleAdvancesAtOnce()
    {
        Transaction first = OpenAdvance(UserId);
        Transaction second = OpenAdvance(UserId, 5_000_000m);
        var handler = CreateHandler(UserId, first, second);
        Transaction? captured = null;
        _dbContext.Transactions.When(x => x.Add(Arg.Any<Transaction>()))
            .Do(x => captured = x.Arg<Transaction>());

        var command = new CreateTransactionCommand(
            new DateOnly(2026, 7, 14), "Anh Huy hoàn tổng", 7_000_000m, 0m, null,
            Food.Id, null, null, null, false, [first.Id, second.Id]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        captured.ShouldNotBeNull();
        first.ReimbursedByTransactionId.ShouldBe(captured.Id);
        second.ReimbursedByTransactionId.ShouldBe(captured.Id);
    }

    [Fact]
    public async Task Handle_AdvanceLinkOnMoneyOut_Fails()
    {
        Transaction advance = OpenAdvance(UserId);
        var handler = CreateHandler(UserId, advance);

        var command = new CreateTransactionCommand(
            new DateOnly(2026, 7, 9), "Sai chiều", 0m, 500_000m, null,
            Food.Id, null, null, null, false, [advance.Id]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.AdvanceLinkInvalid");
    }

    private static Transaction PrepaidCredit(Guid userId) =>
        Transaction.Create(
            userId, new DateOnly(2026, 1, 5), "Sinh hoạt 5 tháng",
            Money.Create(25_000_000m).Value, Money.Zero(), null,
            Food.Id, null, null, null, false,
            true, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 31)).Value;

    [Fact]
    public async Task Handle_PrepaidCredit_StoresRange()
    {
        var handler = CreateHandler(UserId);
        Transaction? captured = null;
        _dbContext.Transactions.When(x => x.Add(Arg.Any<Transaction>()))
            .Do(x => captured = x.Arg<Transaction>());

        var command = new CreateTransactionCommand(
            new DateOnly(2026, 1, 5), "Sinh hoạt 5 tháng", 25_000_000m, 0m, null,
            Food.Id, null, null, null, false, null,
            true, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 31));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        captured.ShouldNotBeNull();
        captured.IsPrepaid.ShouldBeTrue();
        captured.PrepaidFrom.ShouldBe(new DateOnly(2026, 1, 1));
        captured.PrepaidTo.ShouldBe(new DateOnly(2026, 5, 31));
    }

    [Fact]
    public async Task Handle_PrepaidWithoutRange_Fails()
    {
        var handler = CreateHandler(UserId);
        var command = new CreateTransactionCommand(
            new DateOnly(2026, 1, 5), "Sinh hoạt", 25_000_000m, 0m, null,
            Food.Id, null, null, null, false, null, true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.PrepaidRangeRequired");
    }

    [Fact]
    public async Task Handle_PrepaidRangeBackwards_Fails()
    {
        var handler = CreateHandler(UserId);
        var command = new CreateTransactionCommand(
            new DateOnly(2026, 1, 5), "Sinh hoạt", 25_000_000m, 0m, null,
            Food.Id, null, null, null, false, null,
            true, new DateOnly(2026, 5, 31), new DateOnly(2026, 1, 1));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.PrepaidRangeInvalid");
    }

    [Fact]
    public async Task Handle_PrepaidOnMoneyOut_Fails()
    {
        var handler = CreateHandler(UserId);
        var command = new CreateTransactionCommand(
            new DateOnly(2026, 1, 5), "Sai chiều", 0m, 1_000_000m, null,
            Food.Id, null, null, null, false, null,
            true, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 31));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.PrepaidOnlyOnCredit");
    }

    [Fact]
    public async Task Handle_LinkedDebitWithNoAmount_Succeeds()
    {
        Transaction prepaid = PrepaidCredit(UserId);
        var handler = CreateHandler(UserId, prepaid);
        Transaction? captured = null;
        _dbContext.Transactions.When(x => x.Add(Arg.Any<Transaction>()))
            .Do(x => captured = x.Arg<Transaction>());

        var command = new CreateTransactionCommand(
            new DateOnly(2026, 2, 1), "Sinh hoạt tháng 2", 0m, 0m, null,
            Food.Id, null, null, null, false, null,
            false, null, null, prepaid.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        captured.ShouldNotBeNull();
        captured.PrepaidTransactionId.ShouldBe(prepaid.Id);
        captured.Credit.Amount.ShouldBe(0m);
        captured.Debit.Amount.ShouldBe(0m);
    }

    [Fact]
    public async Task Handle_PrepaidLinkNotFound_Fails()
    {
        var handler = CreateHandler(UserId);
        var command = new CreateTransactionCommand(
            new DateOnly(2026, 2, 1), "Sinh hoạt tháng 2", 0m, 0m, null,
            Food.Id, null, null, null, false, null,
            false, null, null, Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.PrepaidNotFound");
    }
}
