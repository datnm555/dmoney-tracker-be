using Application.Abstractions.Messaging;

namespace Application.Banks;

public sealed record CreateBankCommand(string Name) : ICommand<Guid>;

public sealed record UpdateBankCommand(Guid Id, string Name) : ICommand;

public sealed record DeleteBankCommand(Guid Id) : ICommand;

public sealed record SetDefaultBankCommand(Guid Id) : ICommand;

/// <summary>The user's full bank list, highest priority first.</summary>
public sealed record ReorderBanksCommand(IReadOnlyList<Guid> Ids) : ICommand;
