using Application.Abstractions.Messaging;

namespace Application.Accounts;

public sealed record CreateAccountSnapshotCommand(
    Guid AccountId,
    DateOnly Date,
    decimal Balance,
    decimal? StatementAmount = null,
    DateOnly? DueDate = null,
    decimal? InstallmentRemaining = null,
    bool StatementPaid = false,
    string? Note = null) : ICommand<Guid>;

public sealed record UpdateAccountSnapshotCommand(
    Guid Id,
    DateOnly Date,
    decimal Balance,
    decimal? StatementAmount = null,
    DateOnly? DueDate = null,
    decimal? InstallmentRemaining = null,
    bool StatementPaid = false,
    string? Note = null) : ICommand;

public sealed record DeleteAccountSnapshotCommand(Guid Id) : ICommand;
