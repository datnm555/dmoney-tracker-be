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
    string? Note = null,
    bool StatementUntracked = false) : ICommand<Guid>;

public sealed record UpdateAccountSnapshotCommand(
    Guid Id,
    DateOnly Date,
    decimal Balance,
    decimal? StatementAmount = null,
    DateOnly? DueDate = null,
    decimal? InstallmentRemaining = null,
    bool StatementPaid = false,
    string? Note = null,
    bool StatementUntracked = false) : ICommand;

public sealed record DeleteAccountSnapshotCommand(Guid Id) : ICommand;
