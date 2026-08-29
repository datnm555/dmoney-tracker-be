using Application.Abstractions.Messaging;
using Application.Beneficiaries;
using Application.Beneficiaries.Data;
using Application.Categories;
using Application.Categories.Data;
using Application.GoldTypes;
using Application.GoldTypes.Data;
using Application.Plans;
using Application.Plans.Data;
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
        services.AddScoped<ICommandHandler<RefreshTokenCommand, LoginResponse>, RefreshTokenCommandHandler>();
        services.AddScoped<ICommandHandler<LogoutCommand>, LogoutCommandHandler>();
        services.AddScoped<IQueryHandler<GetCreditsQuery, List<CreditResponse>>, GetCreditsQueryHandler>();
        services.AddScoped<ICommandHandler<CreateCategoryCommand, Guid>, CreateCategoryCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateCategoryCommand>, UpdateCategoryCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteCategoryCommand>, DeleteCategoryCommandHandler>();
        services.AddScoped<IQueryHandler<GetCategoriesQuery, List<CategoryResponse>>, GetCategoriesQueryHandler>();
        services.AddScoped<ICommandHandler<CreateSubCategoryCommand, Guid>, CreateSubCategoryCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateSubCategoryCommand>, UpdateSubCategoryCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteSubCategoryCommand>, DeleteSubCategoryCommandHandler>();
        services.AddScoped<IQueryHandler<GetSubCategoriesQuery, List<SubCategoryResponse>>, GetSubCategoriesQueryHandler>();
        services.AddScoped<IQueryHandler<GetDashboardStatsQuery, DashboardStatsResponse>, GetDashboardStatsQueryHandler>();
        services.AddScoped<IQueryHandler<GetPlansQuery, List<PlanResponse>>, GetPlansQueryHandler>();
        services.AddScoped<ICommandHandler<CreatePlanCommand, Guid>, CreatePlanCommandHandler>();
        services.AddScoped<ICommandHandler<UpdatePlanCommand>, UpdatePlanCommandHandler>();
        services.AddScoped<ICommandHandler<DeletePlanCommand>, DeletePlanCommandHandler>();
        services.AddScoped<ICommandHandler<SetDefaultPlanCommand>, SetDefaultPlanCommandHandler>();
        services.AddScoped<IQueryHandler<GetBeneficiariesQuery, List<BeneficiaryResponse>>, GetBeneficiariesQueryHandler>();
        services.AddScoped<ICommandHandler<CreateBeneficiaryCommand, Guid>, CreateBeneficiaryCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateBeneficiaryCommand>, UpdateBeneficiaryCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteBeneficiaryCommand>, DeleteBeneficiaryCommandHandler>();
        services.AddScoped<ICommandHandler<SetDefaultBeneficiaryCommand>, SetDefaultBeneficiaryCommandHandler>();
        services.AddScoped<IQueryHandler<GetGoldTypesQuery, List<GoldTypeResponse>>, GetGoldTypesQueryHandler>();
        services.AddScoped<ICommandHandler<CreateGoldTypeCommand, Guid>, CreateGoldTypeCommandHandler>();
        return services;
    }
}
