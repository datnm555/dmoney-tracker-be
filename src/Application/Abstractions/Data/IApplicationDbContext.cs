using Domain.Beneficiaries;
using Domain.Categories;
using Domain.GoldAcquisitions;
using Domain.GoldTypes;
using Domain.Plans;
using Domain.SubCategories;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstractions.Data;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }

    DbSet<Transaction> Transactions { get; }

    DbSet<SubCategory> SubCategories { get; }

    DbSet<Category> Categories { get; }

    DbSet<Plan> Plans { get; }

    DbSet<Beneficiary> Beneficiaries { get; }

    DbSet<GoldType> GoldTypes { get; }

    DbSet<GoldAcquisition> GoldAcquisitions { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<T> Set<T>() where T : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
