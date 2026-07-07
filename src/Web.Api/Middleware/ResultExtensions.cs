using Microsoft.Extensions.Localization;
using SharedKernel;

namespace Web.Api.Middleware;

/// <summary>
/// Maps a Result/Result&lt;T&gt; into a minimal-API IResult, picking HTTP status from Error.Type.
/// Pass an onSuccess factory when you need a non-default response (e.g. 201 Created for POST).
/// The IStringLocalizer overloads translate Error.Description by Error.Code before mapping.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result, IResult? onSuccess = null) =>
        result.IsSuccess
            ? onSuccess ?? Results.NoContent()
            : MapError(result.Error);

    public static IResult ToHttpResult<TValue>(
        this Result<TValue> result,
        Func<TValue, IResult>? onSuccess = null) =>
        result.IsSuccess
            ? (onSuccess?.Invoke(result.Value) ?? Results.Ok(result.Value))
            : MapError(result.Error);

    public static IResult ToHttpResult(
        this Result result,
        IStringLocalizer localizer,
        IResult? onSuccess = null) =>
        result.IsSuccess
            ? onSuccess ?? Results.NoContent()
            : MapError(Localize(result.Error, localizer));

    public static IResult ToHttpResult<TValue>(
        this Result<TValue> result,
        IStringLocalizer localizer,
        Func<TValue, IResult>? onSuccess = null) =>
        result.IsSuccess
            ? (onSuccess?.Invoke(result.Value) ?? Results.Ok(result.Value))
            : MapError(Localize(result.Error, localizer));

    private static Error Localize(Error error, IStringLocalizer localizer)
    {
        LocalizedString localized = localizer[error.Code];
        return localized.ResourceNotFound
            ? error
            : new Error(error.Code, localized.Value, error.Type);
    }

    private static IResult MapError(Error error) => error.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { error.Code, error.Description }),
        ErrorType.Validation => Results.BadRequest(new { error.Code, error.Description }),
        ErrorType.Conflict => Results.Conflict(new { error.Code, error.Description }),
        ErrorType.Unauthorized => Results.Problem(
            detail: error.Description,
            title: error.Code,
            statusCode: StatusCodes.Status401Unauthorized),
        ErrorType.Problem => Results.Problem(
            detail: error.Description,
            title: error.Code,
            statusCode: StatusCodes.Status400BadRequest),
        _ => Results.Problem(
            detail: error.Description,
            title: error.Code,
            statusCode: StatusCodes.Status500InternalServerError),
    };
}
