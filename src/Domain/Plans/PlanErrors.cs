using SharedKernel;

namespace Domain.Plans;

public static class PlanErrors
{
    public static readonly Error NameRequired = Error.Validation(
        "Plans.NameRequired",
        "Please enter a plan name.");

    public static readonly Error NameTooLong = Error.Validation(
        "Plans.NameTooLong",
        $"Plan name must be at most {PlanConstants.NameMaxLength} characters.");

    public static readonly Error NotFound = Error.NotFound(
        "Plans.NotFound",
        "Plan not found.");

    public static readonly Error NotEmpty = Error.Conflict(
        "Plans.NotEmpty",
        "This plan still has transactions and cannot be deleted.");

    public static readonly Error CannotDeleteDefault = Error.Conflict(
        "Plans.CannotDeleteDefault",
        "The default plan cannot be deleted.");
}
