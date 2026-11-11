using Application.Abstractions.Messaging;

namespace Application.Savings;

/// <summary>Creates or replaces the goal of one savings place ("Nơi gửi").</summary>
public sealed record SetSavingsGoalCommand(
    string SavingsBank,
    decimal TargetAmount,
    string StartPeriod,
    int Months) : ICommand<Guid>;

public sealed record DeleteSavingsGoalCommand(Guid Id) : ICommand;
