using Application.Abstractions.Messaging;

namespace Application.Accounts;

public sealed record CreateAccountCommand(
    string Name,
    string Type,
    string? LastDigits = null,
    decimal? CreditLimit = null,
    int? StatementDay = null,
    int? DueDay = null,
    decimal? InterestRatePercent = null,
    string? BankName = null,
    decimal? UntrackedDebt = null) : ICommand<Guid>;

public sealed record UpdateAccountCommand(
    Guid Id,
    string Name,
    string Type,
    string? LastDigits = null,
    decimal? CreditLimit = null,
    int? StatementDay = null,
    int? DueDay = null,
    decimal? InterestRatePercent = null,
    string? BankName = null,
    decimal? UntrackedDebt = null) : ICommand;

public sealed record DeleteAccountCommand(Guid Id) : ICommand;

public sealed record SetDefaultAccountCommand(Guid Id) : ICommand;

public sealed record ClearDefaultAccountCommand(Guid Id) : ICommand;
