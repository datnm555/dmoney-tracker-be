using Application.Abstractions.Messaging;
using Application.Beneficiaries.Data;

namespace Application.Beneficiaries;

public sealed record GetBeneficiariesQuery : IQuery<List<BeneficiaryResponse>>;
