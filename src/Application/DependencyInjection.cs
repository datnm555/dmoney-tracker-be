using Application.Abstractions.Messaging;
using Application.Categories;
using Application.Categories.Data;
using Application.SubCategories;
using Application.SubCategories.Data;
using Application.Transactions;
using Application.Transactions.Data;
using Application.Users;
using Application.Users.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<RegisterUserCommand, Guid>, RegisterUserCommandHandler>();
        services.AddScoped<ICommandHandler<LoginCommand, LoginResponse>, LoginCommandHandler>();
        services.AddScoped<ICommandHandler<CreateTransactionCommand, Guid>, CreateTransactionCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateTransactionCommand>, UpdateTransactionCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteTransactionCommand>, DeleteTransactionCommandHandler>();
        services.AddScoped<ICommandHandler<ImportTransactionsCommand, int>, ImportTransactionsCommandHandler>();
        services.AddScoped<IQueryHandler<GetTransactionsByMonthQuery, MonthlySummaryResponse>, GetTransactionsByMonthQueryHandler>();
        services.AddScoped<IQueryHandler<GetOpenAdvancesQuery, List<AdvanceResponse>>, GetOpenAdvancesQueryHandler>();
        services.AddScoped<IQueryHandler<GetPrepaidCreditsQuery, List<PrepaidCreditResponse>>, GetPrepaidCreditsQueryHandler>();
        services.AddScoped<IQueryHandler<GetCreditsQuery, List<CreditResponse>>, GetCreditsQueryHandler>();
        services.AddScoped<ICommandHandler<CreateCategoryCommand, Guid>, CreateCategoryCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteCategoryCommand>, DeleteCategoryCommandHandler>();
        services.AddScoped<IQueryHandler<GetCategoriesQuery, List<CategoryResponse>>, GetCategoriesQueryHandler>();
        services.AddScoped<ICommandHandler<CreateSubCategoryCommand, Guid>, CreateSubCategoryCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateSubCategoryCommand>, UpdateSubCategoryCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteSubCategoryCommand>, DeleteSubCategoryCommandHandler>();
        services.AddScoped<IQueryHandler<GetSubCategoriesQuery, List<SubCategoryResponse>>, GetSubCategoriesQueryHandler>();
        services.AddScoped<IQueryHandler<GetDashboardStatsQuery, DashboardStatsResponse>, GetDashboardStatsQueryHandler>();
        return services;
    }
}
