using Application.Abstractions.Data;
using Domain.Beneficiaries;
using Domain.Categories;
using Domain.GoldAcquisitions;
using Domain.GoldTypes;
using Domain.Plans;
using Domain.PurchasePlaces;
using Domain.SubCategories;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database;

internal sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<SubCategory> SubCategories => Set<SubCategory>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Plan> Plans => Set<Plan>();

    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();

    public DbSet<GoldType> GoldTypes => Set<GoldType>();

    public DbSet<PurchasePlace> PurchasePlaces => Set<PurchasePlace>();

    public DbSet<GoldAcquisition> GoldAcquisitions => Set<GoldAcquisition>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
