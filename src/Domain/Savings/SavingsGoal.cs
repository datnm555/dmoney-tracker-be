using Domain.RecurringPayments;
using SharedKernel;

namespace Domain.Savings;

/// <summary>
/// A target for one savings place ("Nơi gửi"): reach <see cref="TargetAmount"/> over
/// <see cref="Months"/> months starting at <see cref="StartPeriod"/> (YYYY-MM).
/// </summary>
public sealed class SavingsGoal : AuditedEntity
{
    public const int SavingsBankMaxLength = 100;
    public const int MaxMonths = 600;

    private SavingsGoal() { }

    public Guid UserId { get; private set; }

    public string SavingsBank { get; private set; } = string.Empty;

    public decimal TargetAmount { get; private set; }

    public string StartPeriod { get; private set; } = string.Empty;

    public int Months { get; private set; }

    /// <summary>Saved before tracking began ("Đã có từ trước"); counts toward the balance without a transaction.</summary>
    public decimal InitialAmount { get; private set; }

    public static Result<SavingsGoal> Create(
        Guid userId, string savingsBank, decimal targetAmount, string startPeriod, int months, decimal initialAmount = 0)
    {
        var goal = new SavingsGoal { Id = Guid.CreateVersion7(), UserId = userId };
        Result applied = goal.Apply(savingsBank, targetAmount, startPeriod, months, initialAmount);
        return applied.IsFailure ? Result.Failure<SavingsGoal>(applied.Error) : goal;
    }

    /// <summary>Changes the target; the savings place stays the goal's identity.</summary>
    public Result Update(decimal targetAmount, string startPeriod, int months, decimal initialAmount = 0)
    {
        var scratch = new SavingsGoal();
        Result validated = scratch.Apply(SavingsBank, targetAmount, startPeriod, months, initialAmount);
        if (validated.IsFailure)
        {
            return validated;
        }

        TargetAmount = scratch.TargetAmount;
        StartPeriod = scratch.StartPeriod;
        Months = scratch.Months;
        InitialAmount = scratch.InitialAmount;
        return Result.Success();
    }

    private Result Apply(string savingsBank, decimal targetAmount, string startPeriod, int months, decimal initialAmount)
    {
        string trimmed = savingsBank?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure(SavingsGoalErrors.SavingsBankRequired);
        }

        if (trimmed.Length > SavingsBankMaxLength)
        {
            return Result.Failure(SavingsGoalErrors.SavingsBankTooLong);
        }

        if (targetAmount <= 0)
        {
            return Result.Failure(SavingsGoalErrors.InvalidTarget);
        }

        if (!RecurringPaymentRecord.TryParsePeriod(startPeriod, out _))
        {
            return Result.Failure(SavingsGoalErrors.InvalidPeriod);
        }

        if (months is < 1 or > MaxMonths)
        {
            return Result.Failure(SavingsGoalErrors.InvalidMonths);
        }

        if (initialAmount < 0)
        {
            return Result.Failure(SavingsGoalErrors.NegativeInitialAmount);
        }

        SavingsBank = trimmed;
        TargetAmount = targetAmount;
        StartPeriod = startPeriod;
        Months = months;
        InitialAmount = initialAmount;
        return Result.Success();
    }
}
