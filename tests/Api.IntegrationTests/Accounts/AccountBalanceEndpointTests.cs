using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Accounts;

/// <summary>Transactions paid with an account ("Trả bằng") move its current balance after the latest reading.</summary>
public sealed class AccountBalanceEndpointTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string username)
    {
        HttpClient client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/users/register",
            new { email, username, displayName = "Test User", password = "password123" });
        register.StatusCode.ShouldBe(HttpStatusCode.OK);

        var login = await client.PostAsJsonAsync("/users/login",
            new { identifier = email, password = "password123" });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await login.Content.ReadFromJsonAsync<LoginBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private sealed record LoginBody(string Token);
    private sealed record CreatedBody(Guid Id);
    private sealed record PlanBody(Guid Id);
    private sealed record MoneyBody(decimal Amount, string Currency);
    private sealed record SnapshotBody(DateOnly Date, MoneyBody Balance);
    private sealed record AccountBody(
        Guid Id, SnapshotBody? LatestSnapshot, MoneyBody CurrentBalance, int TransactionsSinceSnapshot,
        MoneyBody StatementPaidAmount, MoneyBody? UntrackedDebt = null, MoneyBody? UntrackedDebtRemaining = null);
    private sealed record ItemBody(Guid Id, Guid? AccountId, Guid? ToAccountId);
    private sealed record SummaryBody(List<ItemBody> Items);

    private sealed class Ledger(HttpClient client, Guid planId, Guid categoryId)
    {
        public HttpClient Client => client;

        public Guid PlanId => planId;

        public object Payload(string date, decimal debit, decimal credit, Guid? accountId, Guid? toAccountId = null) => new
        {
            date,
            content = "Đóng phạt nguội xe oto",
            creditAmount = credit,
            debitAmount = debit,
            categoryId,
            planId,
            paymentMethod = "transfer",
            bank = "VPBank",
            accountId,
            toAccountId
        };

        public async Task<Guid> AddAsync(
            string date, decimal debit, Guid? accountId, decimal credit = 0m, Guid? toAccountId = null)
        {
            var response = await client.PostAsJsonAsync("/transactions", Payload(date, debit, credit, accountId, toAccountId));
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
        }

        public async Task<Guid> AccountAsync(string type, decimal? balance = null, string date = "2026-11-11")
        {
            var response = await client.PostAsJsonAsync("/accounts", new { name = $"Acc {type}", type });
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            Guid id = (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
            if (balance is { } amount)
            {
                await SnapshotAsync(id, amount, date);
            }

            return id;
        }

        public async Task SnapshotAsync(Guid accountId, decimal balance, string date = "2026-11-11") =>
            (await client.PostAsJsonAsync($"/accounts/{accountId}/snapshots", new { date, balance, statementPaid = false }))
                .StatusCode.ShouldBe(HttpStatusCode.Created);

        public async Task<AccountBody> GetAsync(Guid accountId) =>
            (await client.GetFromJsonAsync<List<AccountBody>>("/accounts"))!.Single(a => a.Id == accountId);
    }

    private async Task<Ledger> LedgerAsync(string email, string username)
    {
        HttpClient client = await CreateAuthenticatedClientAsync(email, username);
        Guid planId = (await client.GetFromJsonAsync<List<PlanBody>>("/plans"))![0].Id;
        var category = await client.PostAsJsonAsync("/categories", new { name = $"Phạt {username}", icon = "car" });
        category.StatusCode.ShouldBe(HttpStatusCode.Created);
        return new Ledger(client, planId, (await category.Content.ReadFromJsonAsync<CreatedBody>())!.Id);
    }

    [Fact]
    public async Task Overdraft_LinkedTransactionsAfterReading_MoveCurrentBalance()
    {
        Ledger ledger = await LedgerAsync("bal-od@example.com", "balod");
        Guid overdraft = await ledger.AccountAsync("overdraft", 144_492_665m);

        await ledger.AddAsync("2026-11-10", 1_000_000m, overdraft); // dated before the reading: already in it
        await ledger.AddAsync("2026-11-11", 700_000m, overdraft); // same day, entered after the reading
        await ledger.AddAsync("2026-11-12", 0m, overdraft, credit: 200_000m); // money in pays the debt down
        await ledger.AddAsync("2026-11-12", 9_999_999m, null); // not paid with the account

        AccountBody account = await ledger.GetAsync(overdraft);
        account.LatestSnapshot!.Balance.Amount.ShouldBe(144_492_665m);
        account.CurrentBalance.Amount.ShouldBe(144_992_665m);
        account.TransactionsSinceSnapshot.ShouldBe(2);
    }

    [Fact]
    public async Task MoneyAccount_SpendsOnDebit_GainsOnCredit_AndNewReadingAbsorbsThem()
    {
        Ledger ledger = await LedgerAsync("bal-chk@example.com", "balchk");
        Guid checking = await ledger.AccountAsync("checking", 5_000_000m);

        await ledger.AddAsync("2026-11-11", 1_500_000m, checking);
        await ledger.AddAsync("2026-11-12", 0m, checking, credit: 300_000m);
        (await ledger.GetAsync(checking)).CurrentBalance.Amount.ShouldBe(3_800_000m);

        await ledger.SnapshotAsync(checking, 3_750_000m, "2026-11-12");
        AccountBody account = await ledger.GetAsync(checking);
        account.CurrentBalance.Amount.ShouldBe(3_750_000m);
        account.TransactionsSinceSnapshot.ShouldBe(0);
    }

    [Fact]
    public async Task AccountWithoutReading_StartsFromZero()
    {
        Ledger ledger = await LedgerAsync("bal-none@example.com", "balnone");
        Guid card = await ledger.AccountAsync("credit_card");

        await ledger.AddAsync("2026-11-01", 450_000m, card);

        AccountBody account = await ledger.GetAsync(card);
        account.LatestSnapshot.ShouldBeNull();
        account.CurrentBalance.Amount.ShouldBe(450_000m);
        account.TransactionsSinceSnapshot.ShouldBe(1);
    }

    [Fact]
    public async Task Update_MovesAndClearsTheLink_AndListReturnsIt()
    {
        Ledger ledger = await LedgerAsync("bal-upd@example.com", "balupd");
        Guid overdraft = await ledger.AccountAsync("overdraft", 100_000_000m);
        Guid checking = await ledger.AccountAsync("checking", 10_000_000m);
        Guid id = await ledger.AddAsync("2026-11-12", 700_000m, overdraft);

        var move = await ledger.Client.PutAsJsonAsync($"/transactions/{id}", ledger.Payload("2026-11-12", 700_000m, 0m, checking));
        move.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ledger.GetAsync(overdraft)).CurrentBalance.Amount.ShouldBe(100_000_000m);
        (await ledger.GetAsync(checking)).CurrentBalance.Amount.ShouldBe(9_300_000m);

        var summary = await ledger.Client.GetFromJsonAsync<SummaryBody>($"/transactions?month=2026-11&planId={ledger.PlanId}");
        summary!.Items.Single(i => i.Id == id).AccountId.ShouldBe(checking);

        var clear = await ledger.Client.PutAsJsonAsync($"/transactions/{id}", ledger.Payload("2026-11-12", 700_000m, 0m, null));
        clear.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ledger.GetAsync(checking)).CurrentBalance.Amount.ShouldBe(10_000_000m);
    }

    [Fact]
    public async Task DeletingTheAccount_KeepsTheTransaction_Unlinked()
    {
        Ledger ledger = await LedgerAsync("bal-del@example.com", "baldel");
        Guid overdraft = await ledger.AccountAsync("overdraft", 1_000_000m);
        Guid id = await ledger.AddAsync("2026-11-12", 700_000m, overdraft);

        (await ledger.Client.DeleteAsync($"/accounts/{overdraft}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var summary = await ledger.Client.GetFromJsonAsync<SummaryBody>($"/transactions?month=2026-11&planId={ledger.PlanId}");
        summary!.Items.Single(i => i.Id == id).AccountId.ShouldBeNull();
    }

    [Fact]
    public async Task OtherUsersAccount_Returns404_OnCreateAndUpdate()
    {
        Ledger owner = await LedgerAsync("bal-own@example.com", "balown");
        Guid ownerAccount = await owner.AccountAsync("overdraft", 1_000_000m);
        Ledger intruder = await LedgerAsync("bal-int@example.com", "balint");

        var create = await intruder.Client.PostAsJsonAsync("/transactions", intruder.Payload("2026-11-12", 700_000m, 0m, ownerAccount));
        create.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await create.Content.ReadAsStringAsync()).ShouldContain("Accounts.NotFound");

        Guid id = await intruder.AddAsync("2026-11-12", 700_000m, null);
        var update = await intruder.Client.PutAsJsonAsync($"/transactions/{id}", intruder.Payload("2026-11-12", 700_000m, 0m, ownerAccount));
        update.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await owner.GetAsync(ownerAccount)).CurrentBalance.Amount.ShouldBe(1_000_000m);
    }

    [Fact]
    public async Task Transfer_PayingACardFromOverdraft_MovesBothAccounts()
    {
        Ledger ledger = await LedgerAsync("bal-xfer@example.com", "balxfer");
        Guid overdraft = await ledger.AccountAsync("overdraft", 128_492_665m);
        Guid card = await ledger.AccountAsync("credit_card", 24_625_403m);
        Guid salary = await ledger.AccountAsync("checking", 1_000_000m);

        Guid id = await ledger.AddAsync("2026-11-11", 16_135_984m, overdraft, toAccountId: card);
        await ledger.AddAsync("2026-11-12", 500_000m, overdraft, toAccountId: salary); // move money to own account

        (await ledger.GetAsync(overdraft)).CurrentBalance.Amount.ShouldBe(145_128_649m);
        AccountBody paidCard = await ledger.GetAsync(card);
        paidCard.CurrentBalance.Amount.ShouldBe(8_489_419m);
        paidCard.TransactionsSinceSnapshot.ShouldBe(1);
        (await ledger.GetAsync(salary)).CurrentBalance.Amount.ShouldBe(1_500_000m);

        var summary = await ledger.Client.GetFromJsonAsync<SummaryBody>($"/transactions?month=2026-11&planId={ledger.PlanId}");
        summary!.Items.Single(i => i.Id == id).ToAccountId.ShouldBe(card);

        var clear = await ledger.Client.PutAsJsonAsync($"/transactions/{id}", ledger.Payload("2026-11-11", 16_135_984m, 0m, overdraft));
        clear.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ledger.GetAsync(card)).CurrentBalance.Amount.ShouldBe(24_625_403m);
    }

    [Fact]
    public async Task Transfer_RejectsSameAccount_MoneyIn_AndOtherUsersAccount()
    {
        Ledger ledger = await LedgerAsync("bal-xbad@example.com", "balxbad");
        Guid overdraft = await ledger.AccountAsync("overdraft", 1_000_000m);
        Guid card = await ledger.AccountAsync("credit_card", 1_000_000m);
        Ledger other = await LedgerAsync("bal-xoth@example.com", "balxoth");
        Guid otherCard = await other.AccountAsync("credit_card", 1_000_000m);

        var same = await ledger.Client.PostAsJsonAsync("/transactions", ledger.Payload("2026-11-12", 100m, 0m, overdraft, overdraft));
        same.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await same.Content.ReadAsStringAsync()).ShouldContain("Transactions.TransferSameAccount");

        var moneyIn = await ledger.Client.PostAsJsonAsync("/transactions", ledger.Payload("2026-11-12", 0m, 100m, overdraft, card));
        moneyIn.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await moneyIn.Content.ReadAsStringAsync()).ShouldContain("Transactions.TransferDebitOnly");

        var foreign = await ledger.Client.PostAsJsonAsync("/transactions", ledger.Payload("2026-11-12", 100m, 0m, overdraft, otherCard));
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        Guid id = await ledger.AddAsync("2026-11-12", 100m, overdraft);
        var update = await ledger.Client.PutAsJsonAsync($"/transactions/{id}", ledger.Payload("2026-11-12", 100m, 0m, overdraft, otherCard));
        update.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.GetAsync(otherCard)).CurrentBalance.Amount.ShouldBe(1_000_000m);
    }

    [Fact]
    public async Task StatementPaidAmount_ComesFromTransfersIntoTheCard_AndUndoesWithThem()
    {
        Ledger ledger = await LedgerAsync("bal-paid@example.com", "balpaid");
        Guid card = await ledger.AccountAsync("credit_card");
        (await ledger.Client.PostAsJsonAsync($"/accounts/{card}/snapshots", new
        {
            date = "2026-11-10", balance = 90_678_586m, statementAmount = 65_652_975m, dueDate = "2026-11-13", statementPaid = false
        })).StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid checking = await ledger.AccountAsync("checking", 70_000_000m, "2026-11-10");

        // A swipe on the card is new spending, not a payment.
        await ledger.AddAsync("2026-11-11", 200_000m, card);
        Guid part = await ledger.AddAsync("2026-11-12", 40_000_000m, checking, toAccountId: card);
        await ledger.AddAsync("2026-11-13", 25_652_975m, checking, toAccountId: card);

        AccountBody paid = await ledger.GetAsync(card);
        paid.StatementPaidAmount.Amount.ShouldBe(65_652_975m);
        paid.CurrentBalance.Amount.ShouldBe(25_225_611m);
        paid.LatestSnapshot!.Balance.Amount.ShouldBe(90_678_586m);
        (await ledger.GetAsync(checking)).CurrentBalance.Amount.ShouldBe(4_347_025m);

        // Deleting a payment puts both the debt and the open statement back.
        (await ledger.Client.DeleteAsync($"/transactions/{part}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        AccountBody reopened = await ledger.GetAsync(card);
        reopened.StatementPaidAmount.Amount.ShouldBe(25_652_975m);
        reopened.CurrentBalance.Amount.ShouldBe(65_225_611m);
        (await ledger.GetAsync(checking)).CurrentBalance.Amount.ShouldBe(44_347_025m);
    }

    [Fact]
    public async Task AccountTransactions_ListLinkedRows_WithTheirEffectAndWhetherTheyMoveTheBalance()
    {
        Ledger ledger = await LedgerAsync("bal-list@example.com", "ballist");
        Guid overdraft = await ledger.AccountAsync("overdraft", 1_000_000m, "2026-11-10");
        Guid checking = await ledger.AccountAsync("checking", 5_000_000m, "2026-11-10");
        Guid before = await ledger.AddAsync("2026-11-09", 100_000m, overdraft);
        Guid spent = await ledger.AddAsync("2026-11-11", 200_000m, overdraft);
        Guid paidIn = await ledger.AddAsync("2026-11-12", 300_000m, checking, toAccountId: overdraft);
        await ledger.AddAsync("2026-11-12", 50_000m, checking);

        List<AccountTxBody> rows = (await ledger.Client.GetFromJsonAsync<List<AccountTxBody>>($"/accounts/{overdraft}/transactions"))!;

        rows.Select(r => r.Id).ShouldBe([paidIn, spent, before]);
        rows[0].Incoming.ShouldBeTrue();
        rows[0].Effect.Amount.ShouldBe(-300_000m);
        rows[0].AfterSnapshot.ShouldBeTrue();
        rows[1].Effect.Amount.ShouldBe(200_000m);
        rows[1].AfterSnapshot.ShouldBeTrue();
        rows[2].AfterSnapshot.ShouldBeFalse();
        (await ledger.GetAsync(overdraft)).CurrentBalance.Amount.ShouldBe(1_000_000m + rows.Where(r => r.AfterSnapshot).Sum(r => r.Effect.Amount));

        Ledger other = await LedgerAsync("bal-list2@example.com", "ballist2");
        (await other.Client.GetAsync($"/accounts/{overdraft}/transactions")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Import_IntoAnAccount_LinksEveryRow()
    {
        Ledger ledger = await LedgerAsync("bal-imp@example.com", "balimp");
        Guid checking = await ledger.AccountAsync("checking", 5_000_000m, "2026-11-10");

        var import = await ledger.Client.PostAsJsonAsync("/transactions/import", new
        {
            planId = ledger.PlanId,
            accountId = checking,
            rows = new[]
            {
                new { date = "2026-11-11", content = "Lương", amount = 2_000_000m, note = (string?)null },
                new { date = "2026-11-12", content = "Grab", amount = -150_000m, note = (string?)null }
            }
        });
        import.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ledger.GetAsync(checking)).CurrentBalance.Amount.ShouldBe(6_850_000m);

        Ledger other = await LedgerAsync("bal-imp2@example.com", "balimp2");
        var foreign = await other.Client.PostAsJsonAsync("/transactions/import", new
        {
            planId = other.PlanId,
            accountId = checking,
            rows = new[] { new { date = "2026-11-11", content = "X", amount = 1m, note = (string?)null } }
        });
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static object CardBody(decimal? untrackedDebt, string type = "credit_card") =>
        new { name = "Thẻ tín dụng Diamond", type, untrackedDebt };

    [Fact]
    public async Task UntrackedDebt_PaymentsEnteredAfterItPayItOffFirst()
    {
        Ledger ledger = await LedgerAsync("bal-old@example.com", "balold");
        Guid overdraft = await ledger.AccountAsync("overdraft", 144_492_665m);
        Guid card = await ledger.AccountAsync("credit_card", 24_625_403m);
        // Paid before the old debt was stated: already left out of it.
        await ledger.AddAsync("2026-11-11", 16_135_984m, overdraft, toAccountId: card);

        (await ledger.Client.PutAsJsonAsync($"/accounts/{card}", CardBody(8_489_419m)))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        AccountBody stated = await ledger.GetAsync(card);
        stated.UntrackedDebt!.Amount.ShouldBe(8_489_419m);
        stated.UntrackedDebtRemaining!.Amount.ShouldBe(8_489_419m);

        await ledger.AddAsync("2026-11-12", 132_000m, card); // a new swipe is not a payment
        Guid first = await ledger.AddAsync("2026-11-20", 5_000_000m, overdraft, toAccountId: card);
        (await ledger.GetAsync(card)).UntrackedDebtRemaining!.Amount.ShouldBe(3_489_419m);

        // Restating the same amount keeps counting from when it was first set.
        (await ledger.Client.PutAsJsonAsync($"/accounts/{card}", CardBody(8_489_419m)))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ledger.GetAsync(card)).UntrackedDebtRemaining!.Amount.ShouldBe(3_489_419m);

        await ledger.AddAsync("2026-12-11", 3_621_419m, overdraft, toAccountId: card);
        (await ledger.GetAsync(card)).UntrackedDebtRemaining!.Amount.ShouldBe(0m);

        // Deleting a payment puts the old debt back.
        (await ledger.Client.DeleteAsync($"/transactions/{first}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ledger.GetAsync(card)).UntrackedDebtRemaining!.Amount.ShouldBe(4_868_000m);
    }

    [Fact]
    public async Task UntrackedDebt_CountsPaymentsDatedBeforeTheLatestReading()
    {
        Ledger ledger = await LedgerAsync("bal-oldr@example.com", "baloldr");
        Guid checking = await ledger.AccountAsync("checking", 10_000_000m, "2026-11-12");
        var create = await ledger.Client.PostAsJsonAsync("/accounts", CardBody(2_000_000m));
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid card = (await create.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
        await ledger.SnapshotAsync(card, 2_000_000m, "2026-11-12");

        // Entered late but dated before the readings: inside them, yet it still paid old debt.
        await ledger.AddAsync("2026-11-10", 500_000m, checking, toAccountId: card);

        AccountBody account = await ledger.GetAsync(card);
        account.CurrentBalance.Amount.ShouldBe(2_000_000m);
        account.UntrackedDebtRemaining!.Amount.ShouldBe(1_500_000m);
    }

    [Fact]
    public async Task UntrackedDebt_OnlyOnCards_AndNeverNegative()
    {
        Ledger ledger = await LedgerAsync("bal-oldx@example.com", "baloldx");
        var overdraft = await ledger.Client.PostAsJsonAsync("/accounts", CardBody(1_000_000m, "overdraft"));
        overdraft.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid id = (await overdraft.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
        AccountBody account = await ledger.GetAsync(id);
        account.UntrackedDebt.ShouldBeNull();
        account.UntrackedDebtRemaining.ShouldBeNull();

        var negative = await ledger.Client.PostAsJsonAsync("/accounts", CardBody(-1m));
        negative.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await negative.Content.ReadAsStringAsync()).ShouldContain("Accounts.NegativeAmount");
    }

    private sealed record AccountTxBody(Guid Id, MoneyBody Effect, bool Incoming, bool AfterSnapshot);
}
