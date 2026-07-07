using Domain.Transactions;
using SharedKernel;
using Shouldly;

namespace Application.UnitTests.Transactions;

public class TransactionTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 7, 6);

    private static Money Vnd(decimal amount) => Money.Create(amount).Value;

    [Fact]
    public void Create_WithValidInput_TrimsContentAndNote()
    {
        var result = Transaction.Create(UserId, Date, "  Lương tháng 7  ", Vnd(15_000_000m), Money.Zero(), "  chuyển khoản  ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Content.ShouldBe("Lương tháng 7");
        result.Value.Note.ShouldBe("chuyển khoản");
        result.Value.UserId.ShouldBe(UserId);
        result.Value.Date.ShouldBe(Date);
        result.Value.Credit.Amount.ShouldBe(15_000_000m);
        result.Value.Debit.Amount.ShouldBe(0m);
    }

    [Fact]
    public void Create_WithEmptyNote_StoresNull()
    {
        var result = Transaction.Create(UserId, Date, "Ăn trưa", Money.Zero(), Vnd(50_000m), "   ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Note.ShouldBeNull();
    }

    [Fact]
    public void Create_WithBlankContent_Fails()
    {
        var result = Transaction.Create(UserId, Date, "  ", Vnd(1m), Money.Zero(), null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.ContentRequired");
    }

    [Fact]
    public void Create_WithContentOver500Chars_Fails()
    {
        var result = Transaction.Create(UserId, Date, new string('x', 501), Vnd(1m), Money.Zero(), null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.ContentTooLong");
    }

    [Fact]
    public void Create_WithNoteOver1000Chars_Fails()
    {
        var result = Transaction.Create(UserId, Date, "ok", Vnd(1m), Money.Zero(), new string('x', 1001));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.NoteTooLong");
    }

    [Fact]
    public void Create_WithBothAmountsZero_Fails()
    {
        var result = Transaction.Create(UserId, Date, "ok", Money.Zero(), Money.Zero(), null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.EmptyAmount");
    }

    [Fact]
    public void Create_WithBothAmountsPositive_Succeeds()
    {
        var result = Transaction.Create(UserId, Date, "hoàn tiền một phần", Vnd(100_000m), Vnd(250_000m), null);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Update_WithValidInput_ReplacesAllFields()
    {
        Transaction transaction = Transaction.Create(UserId, Date, "cũ", Vnd(1_000m), Money.Zero(), null).Value;
        var newDate = new DateOnly(2026, 7, 10);

        var result = transaction.Update(newDate, "mới", Money.Zero(), Vnd(2_000m), "note mới");

        result.IsSuccess.ShouldBeTrue();
        transaction.Date.ShouldBe(newDate);
        transaction.Content.ShouldBe("mới");
        transaction.Credit.Amount.ShouldBe(0m);
        transaction.Debit.Amount.ShouldBe(2_000m);
        transaction.Note.ShouldBe("note mới");
    }

    [Fact]
    public void Update_WithBothAmountsZero_FailsAndKeepsOldValues()
    {
        Transaction transaction = Transaction.Create(UserId, Date, "cũ", Vnd(1_000m), Money.Zero(), null).Value;

        var result = transaction.Update(Date, "mới", Money.Zero(), Money.Zero(), null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.EmptyAmount");
        transaction.Content.ShouldBe("cũ");
        transaction.Credit.Amount.ShouldBe(1_000m);
    }

    [Fact]
    public void Create_WithValidCategory_StoresIt()
    {
        var result = Transaction.Create(UserId, Date, "Ăn trưa", Money.Zero(), Vnd(50_000m), null, "food");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Category.ShouldBe("food");
    }

    [Fact]
    public void Create_WithoutCategory_DefaultsToNull()
    {
        var result = Transaction.Create(UserId, Date, "Lương", Vnd(1m), Money.Zero(), null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Category.ShouldBeNull();
    }

    [Fact]
    public void Create_WithWhitespaceCategory_StoresNull()
    {
        var result = Transaction.Create(UserId, Date, "x", Vnd(1m), Money.Zero(), null, "   ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Category.ShouldBeNull();
    }

    [Fact]
    public void Create_WithUnknownCategory_Fails()
    {
        var result = Transaction.Create(UserId, Date, "x", Vnd(1m), Money.Zero(), null, "crypto");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.InvalidCategory");
    }

    [Fact]
    public void Update_WithUnknownCategory_FailsAndKeepsOldValues()
    {
        Transaction transaction = Transaction.Create(UserId, Date, "cũ", Vnd(1_000m), Money.Zero(), null, "food").Value;

        var result = transaction.Update(Date, "mới", Vnd(2_000m), Money.Zero(), null, "crypto");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Transactions.InvalidCategory");
        transaction.Content.ShouldBe("cũ");
        transaction.Category.ShouldBe("food");
    }

    [Fact]
    public void Update_WithNullCategory_ClearsIt()
    {
        Transaction transaction = Transaction.Create(UserId, Date, "x", Vnd(1m), Money.Zero(), null, "food").Value;

        var result = transaction.Update(Date, "x", Vnd(1m), Money.Zero(), null, null);

        result.IsSuccess.ShouldBeTrue();
        transaction.Category.ShouldBeNull();
    }

    [Fact]
    public void Create_WithoutPaymentMethod_DefaultsToTransfer()
    {
        var result = Transaction.Create(
            Guid.NewGuid(), new DateOnly(2026, 7, 7), "Lunch",
            Money.Zero(), Vnd(50_000m), null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PaymentMethod.ShouldBe(PaymentMethods.Transfer);
        result.Value.CardType.ShouldBeNull();
        result.Value.Bank.ShouldBeNull();
    }

    [Fact]
    public void Create_CardWithTypeAndBank_Succeeds()
    {
        var result = Transaction.Create(
            Guid.NewGuid(), new DateOnly(2026, 7, 7), "Netflix",
            Money.Zero(), Vnd(260_000m), null,
            "entertainment", PaymentMethods.Card, CardTypes.Visa, "Techcombank");

        result.IsSuccess.ShouldBeTrue();
        result.Value.PaymentMethod.ShouldBe(PaymentMethods.Card);
        result.Value.CardType.ShouldBe(CardTypes.Visa);
        result.Value.Bank.ShouldBe("Techcombank");
    }

    [Fact]
    public void Create_CardWithoutCardType_Fails()
    {
        var result = Transaction.Create(
            Guid.NewGuid(), new DateOnly(2026, 7, 7), "Netflix",
            Money.Zero(), Vnd(260_000m), null,
            null, PaymentMethods.Card);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransactionErrors.CardTypeRequired);
    }

    [Fact]
    public void Create_UnknownPaymentMethod_Fails()
    {
        var result = Transaction.Create(
            Guid.NewGuid(), new DateOnly(2026, 7, 7), "Lunch",
            Money.Zero(), Vnd(50_000m), null,
            null, "crypto");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransactionErrors.InvalidPaymentMethod);
    }

    [Fact]
    public void Create_CardDetailsOnNonCardMethod_Fails()
    {
        var result = Transaction.Create(
            Guid.NewGuid(), new DateOnly(2026, 7, 7), "Lunch",
            Money.Zero(), Vnd(50_000m), null,
            null, PaymentMethods.Cash, CardTypes.Visa);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransactionErrors.CardDetailsNotAllowed);
    }

    [Fact]
    public void Create_UnknownCardType_Fails()
    {
        var result = Transaction.Create(
            Guid.NewGuid(), new DateOnly(2026, 7, 7), "Netflix",
            Money.Zero(), Vnd(260_000m), null,
            null, PaymentMethods.Card, "amex");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TransactionErrors.InvalidCardType);
    }

    [Fact]
    public void Update_CanChangePaymentMethod()
    {
        Transaction transaction = Transaction.Create(
            Guid.NewGuid(), new DateOnly(2026, 7, 7), "Lunch",
            Money.Zero(), Vnd(50_000m), null).Value;

        Result result = transaction.Update(
            transaction.Date, transaction.Content, Money.Zero(),
            Vnd(50_000m), null,
            null, PaymentMethods.Card, CardTypes.Credit, "VPBank");

        result.IsSuccess.ShouldBeTrue();
        transaction.PaymentMethod.ShouldBe(PaymentMethods.Card);
        transaction.CardType.ShouldBe(CardTypes.Credit);
        transaction.Bank.ShouldBe("VPBank");
    }
}
