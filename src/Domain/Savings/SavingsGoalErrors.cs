using SharedKernel;

namespace Domain.Savings;

public static class SavingsGoalErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "SavingsGoals.NotFound", "Savings goal not found.");

    public static readonly Error SavingsBankRequired = Error.Validation(
        "SavingsGoals.SavingsBankRequired", "Choose where the savings are held.");

    public static readonly Error SavingsBankTooLong = Error.Validation(
        "SavingsGoals.SavingsBankTooLong",
        $"Savings place must be at most {SavingsGoal.SavingsBankMaxLength} characters.");

    public static readonly Error InvalidTarget = Error.Validation(
        "SavingsGoals.InvalidTarget", "Target amount must be greater than zero.");

    public static readonly Error InvalidPeriod = Error.Validation(
        "SavingsGoals.InvalidPeriod", "Start month must be in the format YYYY-MM.");

    public static readonly Error InvalidMonths = Error.Validation(
        "SavingsGoals.InvalidMonths", $"Duration must be between 1 and {SavingsGoal.MaxMonths} months.");
}
