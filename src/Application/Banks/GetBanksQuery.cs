using Application.Abstractions.Messaging;
using Application.Banks.Data;

namespace Application.Banks;

public sealed record GetBanksQuery : IQuery<List<BankResponse>>;
