using Domain.Categories;
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

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<T> Set<T>() where T : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
