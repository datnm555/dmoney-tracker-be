using System.Net;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Resources;

public sealed class ResourcesEndpointTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetResources_DefaultsToVietnamese()
    {
        var response = await _client.GetAsync("/resources");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resources = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        resources.ShouldNotBeNull();
        resources["menu.summary"].ShouldBe("Tổng hợp");
        resources["Transactions.EmptyAmount"].ShouldBe("Cần ít nhất một số tiền lớn hơn 0.");
        resources["cat.excludeFromStats"].ShouldBe("Không tính vào thống kê");
        resources["summary.excludedBadge"].ShouldBe("Không tính");
        resources["menu.accounts"].ShouldBe("Tài khoản & Nợ");
        resources["accounts.title"].ShouldBe("Tài khoản & Nợ");
        resources["accounts.hint"].ShouldBe("Số dư nhập tay theo app ngân hàng — không tính từ giao dịch.");
        resources["accounts.cashTotal"].ShouldBe("Tiền đang có");
        resources["accounts.debtTotal"].ShouldBe("Tổng nợ");
        resources["accounts.availableCredit"].ShouldBe("Hạn mức còn dùng được");
        resources["accounts.monthlyInterest"].ShouldBe("Lãi thấu chi ước tính / tháng");
        resources["accounts.upcoming"].ShouldBe("Sắp đến hạn");
        resources["accounts.left"].ShouldBe("còn");
        resources["accounts.days"].ShouldBe("ngày");
        resources["accounts.dueToday"].ShouldBe("hôm nay");
        resources["accounts.markPaid"].ShouldBe("Đã trả");
        resources["accounts.groupMoney"].ShouldBe("Tiền");
        resources["accounts.groupOverdraft"].ShouldBe("Thấu chi");
        resources["accounts.groupCredit"].ShouldBe("Thẻ tín dụng");
        resources["accounts.add"].ShouldBe("Thêm tài khoản");
        resources["accounts.edit"].ShouldBe("Sửa tài khoản");
        resources["accounts.deleteConfirm"].ShouldBe("Xoá tài khoản này và toàn bộ lịch sử số dư?");
        resources["accounts.name"].ShouldBe("Tên");
        resources["accounts.type"].ShouldBe("Loại");
        resources["accounts.type.checking"].ShouldBe("Thanh toán");
        resources["accounts.type.overdraft"].ShouldBe("Thấu chi");
        resources["accounts.type.credit_card"].ShouldBe("Thẻ tín dụng");
        resources["accounts.type.ewallet"].ShouldBe("Ví điện tử");
        resources["accounts.type.cash"].ShouldBe("Tiền mặt");
        resources["accounts.bank"].ShouldBe("Ngân hàng");
        resources["accounts.noBank"].ShouldBe("Khác");
        resources["accounts.lastDigits"].ShouldBe("4 số cuối");
        resources["accounts.creditLimit"].ShouldBe("Hạn mức");
        resources["accounts.statementDay"].ShouldBe("Ngày sao kê");
        resources["accounts.dueDay"].ShouldBe("Ngày đến hạn");
        resources["accounts.interestRate"].ShouldBe("Lãi suất (%/năm)");
        resources["accounts.updateBalance"].ShouldBe("Cập nhật số dư");
        resources["accounts.history"].ShouldBe("Lịch sử số dư");
        resources["accounts.date"].ShouldBe("Ngày");
        resources["accounts.balance"].ShouldBe("Số dư");
        resources["accounts.owed"].ShouldBe("Dư nợ");
        resources["accounts.available"].ShouldBe("Khả dụng");
        resources["accounts.statementAmount"].ShouldBe("Phải trả kỳ này");
        resources["accounts.dueDate"].ShouldBe("Hạn thanh toán");
        resources["accounts.installmentRemaining"].ShouldBe("Trả góp còn lại");
        resources["accounts.statementPaid"].ShouldBe("Đã thanh toán kỳ này");
        resources["accounts.note"].ShouldBe("Ghi chú");
        resources["accounts.lastUpdated"].ShouldBe("Cập nhật");
        resources["accounts.empty"].ShouldBe("Chưa có tài khoản nào");
        resources["accounts.noSnapshot"].ShouldBe("Chưa cập nhật số dư");
        resources["accounts.dueIn"].ShouldBe("đến hạn sau");
        resources["accounts.dueTodayFull"].ShouldBe("đến hạn hôm nay");
        resources["accounts.overdue"].ShouldBe("quá hạn");
        resources["accounts.deleteSnapshotConfirm"].ShouldBe("Xoá lần cập nhật số dư này?");
    }

    [Fact]
    public async Task GetResources_WithLangEn_ReturnsEnglish()
    {
        var response = await _client.GetAsync("/resources?lang=en");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resources = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        resources.ShouldNotBeNull();
        resources["menu.summary"].ShouldBe("Summary");
        resources["cat.excludeFromStats"].ShouldBe("Exclude from stats");
        resources["summary.excludedBadge"].ShouldBe("Not counted");
        resources["menu.accounts"].ShouldBe("Accounts & Debt");
        resources["accounts.title"].ShouldBe("Accounts & Debt");
        resources["accounts.hint"].ShouldBe("Balances are entered from your bank app — not computed from transactions.");
        resources["accounts.cashTotal"].ShouldBe("Cash on hand");
        resources["accounts.debtTotal"].ShouldBe("Total debt");
        resources["accounts.availableCredit"].ShouldBe("Available credit");
        resources["accounts.monthlyInterest"].ShouldBe("Est. overdraft interest / month");
        resources["accounts.upcoming"].ShouldBe("Due soon");
        resources["accounts.left"].ShouldBe("in");
        resources["accounts.days"].ShouldBe("days");
        resources["accounts.dueToday"].ShouldBe("today");
        resources["accounts.markPaid"].ShouldBe("Mark paid");
        resources["accounts.groupMoney"].ShouldBe("Money");
        resources["accounts.groupOverdraft"].ShouldBe("Overdraft");
        resources["accounts.groupCredit"].ShouldBe("Credit cards");
        resources["accounts.add"].ShouldBe("Add account");
        resources["accounts.edit"].ShouldBe("Edit account");
        resources["accounts.deleteConfirm"].ShouldBe("Delete this account and its balance history?");
        resources["accounts.name"].ShouldBe("Name");
        resources["accounts.type"].ShouldBe("Type");
        resources["accounts.type.checking"].ShouldBe("Checking");
        resources["accounts.type.overdraft"].ShouldBe("Overdraft");
        resources["accounts.type.credit_card"].ShouldBe("Credit card");
        resources["accounts.type.ewallet"].ShouldBe("E-wallet");
        resources["accounts.type.cash"].ShouldBe("Cash");
        resources["accounts.bank"].ShouldBe("Bank");
        resources["accounts.noBank"].ShouldBe("Other");
        resources["accounts.lastDigits"].ShouldBe("Last 4 digits");
        resources["accounts.creditLimit"].ShouldBe("Credit limit");
        resources["accounts.statementDay"].ShouldBe("Statement day");
        resources["accounts.dueDay"].ShouldBe("Due day");
        resources["accounts.interestRate"].ShouldBe("Interest rate (%/yr)");
        resources["accounts.updateBalance"].ShouldBe("Update balance");
        resources["accounts.history"].ShouldBe("Balance history");
        resources["accounts.date"].ShouldBe("Date");
        resources["accounts.balance"].ShouldBe("Balance");
        resources["accounts.owed"].ShouldBe("Amount owed");
        resources["accounts.available"].ShouldBe("Available");
        resources["accounts.statementAmount"].ShouldBe("Statement amount");
        resources["accounts.dueDate"].ShouldBe("Due date");
        resources["accounts.installmentRemaining"].ShouldBe("Installments remaining");
        resources["accounts.statementPaid"].ShouldBe("Statement paid");
        resources["accounts.note"].ShouldBe("Note");
        resources["accounts.lastUpdated"].ShouldBe("Updated");
        resources["accounts.empty"].ShouldBe("No accounts yet");
        resources["accounts.noSnapshot"].ShouldBe("No balance yet");
        resources["accounts.dueIn"].ShouldBe("due in");
        resources["accounts.dueTodayFull"].ShouldBe("due today");
        resources["accounts.overdue"].ShouldBe("overdue");
        resources["accounts.deleteSnapshotConfirm"].ShouldBe("Delete this balance reading?");
    }

    [Fact]
    public async Task Login_Error_IsLocalizedByLangParam()
    {
        var vi = await _client.PostAsJsonAsync("/users/login?lang=vi",
            new { identifier = "ghost@example.com", password = "password123" });
        var viBody = await vi.Content.ReadAsStringAsync();
        viBody.ShouldContain("Thông tin đăng nhập không đúng.");

        var en = await _client.PostAsJsonAsync("/users/login?lang=en",
            new { identifier = "ghost@example.com", password = "password123" });
        var enBody = await en.Content.ReadAsStringAsync();
        enBody.ShouldContain("Invalid credentials.");
    }
}
