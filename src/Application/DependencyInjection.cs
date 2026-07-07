using Application.Abstractions.Messaging;
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
        services.AddScoped<IQueryHandler<GetTransactionsByMonthQuery, MonthlySummaryResponse>, GetTransactionsByMonthQueryHandler>();
        services.AddScoped<IQueryHandler<GetDashboardStatsQuery, DashboardStatsResponse>, GetDashboardStatsQueryHandler>();
        return services;
    }
}
