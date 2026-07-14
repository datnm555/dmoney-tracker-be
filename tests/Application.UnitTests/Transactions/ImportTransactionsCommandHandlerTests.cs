using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Transactions;
using Domain.Transactions;
using MockQueryable.NSubstitute;
using NSubstitute;
using SharedKernel;
using Shouldly;

namespace Application.UnitTests.Transactions;

public class ImportTransactionsCommandHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private IApplicationDbContext _dbContext = null!;
    private IUserContext _userContext = null!;

    private ImportTransactionsCommandHandler CreateHandler(Guid? userId)
    {
        _dbContext = Substitute.For<IApplicationDbContext>();
        _userContext = Substitute.For<IUserContext>();

        _userContext.UserId.Returns(userId);
        var transactionsDbSet = new List<Transaction>().BuildMockDbSet();
        _dbContext.Transactions.Returns(transactionsDbSet);
        var categoriesDbSet = new List<Domain.Categories.Category>().BuildMockDbSet();
        _dbContext.Categories.Returns(categoriesDbSet);

        return new ImportTransactionsCommandHandler(_dbContext, _userContext);
    }

    private static ImportTransactionRow Row(decimal amount, string content = "Row") =>
        new(new DateOnly(2026, 7, 8), content, amount, null);

    [Fact]
    public async Task Handle_MapsSignToCreditAndDebit_AndDefaultsCategoryToOther()
    {
        var handler = CreateHandler(UserId);
        var captured = new List<Transaction>();
        _dbContext.Transactions.When(x => x.Add(Arg.Any<Transaction>()))
            .Do(x => captured.Add(x.Arg<Transaction>()));

        var command = new ImportTransactionsCommand([Row(28_000_000m, "Lương"), Row(-1_200_000m, "Tiền điện")]);

        Result<int> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
        captured.Count.ShouldBe(2);
        captured[0].Credit.Amount.ShouldBe(28_000_000m);
        captured[0].Debit.Amount.ShouldBe(0m);
        captured[1].Credit.Amount.ShouldBe(0m);
        captured[1].Debit.Amount.ShouldBe(1_200_000m);
        captured.ShouldAllBe(t => t.Category == TransactionCategories.Other);
        captured.ShouldAllBe(t => t.UserId == UserId);
        captured.ShouldAllBe(t => t.PaymentMethod == PaymentMethods.Transfer);
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutUser_FailsUnauthenticated()
    {
        var handler = CreateHandler(null);

        var result = await handler.Handle(new ImportTransactionsCommand([Row(1000m)]), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.Unauthenticated");
    }

    [Fact]
    public async Task Handle_EmptyRows_Fails()
    {
        var handler = CreateHandler(UserId);

        var result = await handler.Handle(new ImportTransactionsCommand([]), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransactionErrors.ImportEmpty);
        await _dbContext.DidNotReceiveWithAnyArgs().SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_TooManyRows_Fails()
    {
        var handler = CreateHandler(UserId);
        var rows = Enumerable.Range(0, TransactionConstants.ImportMaxRows + 1)
            .Select(i => Row(1000m, $"Row {i}"))
            .ToList();

        var result = await handler.Handle(new ImportTransactionsCommand(rows), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransactionErrors.ImportTooManyRows);
        await _dbContext.DidNotReceiveWithAnyArgs().SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_ZeroAmountRow_FailsWithoutSaving()
    {
        var handler = CreateHandler(UserId);

        var result = await handler.Handle(
            new ImportTransactionsCommand([Row(1000m), Row(0m, "Zero")]), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.EmptyAmount");
        await _dbContext.DidNotReceiveWithAnyArgs().SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_EmptyContentRow_FailsWithoutSaving()
    {
        var handler = CreateHandler(UserId);

        var result = await handler.Handle(
            new ImportTransactionsCommand([Row(1000m, "  ")]), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.ContentRequired");
        await _dbContext.DidNotReceiveWithAnyArgs().SaveChangesAsync();
    }
}
