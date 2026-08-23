using Application.Abstractions.Messaging;

namespace Application.Beneficiaries;

public sealed record CreateBeneficiaryCommand(string Name) : ICommand<Guid>;

public sealed record UpdateBeneficiaryCommand(Guid Id, string Name) : ICommand;

public sealed record DeleteBeneficiaryCommand(Guid Id) : ICommand;

public sealed record SetDefaultBeneficiaryCommand(Guid Id) : ICommand;
