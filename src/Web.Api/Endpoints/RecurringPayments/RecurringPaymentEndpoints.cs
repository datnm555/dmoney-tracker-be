using Application.Abstractions.Messaging;
using Application.RecurringPayments;
using Application.RecurringPayments.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.RecurringPayments;

internal sealed class GetRecurringPayments : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/recurring-payments", async (
            IQueryHandler<GetRecurringPaymentsQuery, List<RecurringPaymentResponse>> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<List<RecurringPaymentResponse>> result = await handler.Handle(
                new GetRecurringPaymentsQuery(), cancellationToken);

            return result.ToHttpResult(localizer, Results.Ok);
        }).RequireAuthorization();
    }
}

internal sealed class CreateRecurringPayment : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/recurring-payments", async (
            CreateRecurringPaymentCommand command,
            ICommandHandler<CreateRecurringPaymentCommand, Guid> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(
                localizer,
                id => Results.Created($"/recurring-payments/{id}", new { id }));
        }).RequireAuthorization();
    }
}

internal sealed class UpdateRecurringPayment : IEndpoint
{
    internal sealed record UpdateRecurringPaymentRequest(
        string Name,
        Guid? CategoryId,
        Guid? SubCategoryId,
        decimal Amount,
        bool IsVariable,
        int DayOfMonth,
        string? PaymentMethod = null,
        string? Bank = null,
        string? Note = null,
        bool IsActive = true);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/recurring-payments/{id:guid}", async (
            Guid id,
            UpdateRecurringPaymentRequest request,
            ICommandHandler<UpdateRecurringPaymentCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new UpdateRecurringPaymentCommand(
                    id,
                    request.Name,
                    request.CategoryId,
                    request.SubCategoryId,
                    request.Amount,
                    request.IsVariable,
                    request.DayOfMonth,
                    request.PaymentMethod,
                    request.Bank,
                    request.Note,
                    request.IsActive),
                cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}

internal sealed class DeleteRecurringPayment : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/recurring-payments/{id:guid}", async (
            Guid id,
            ICommandHandler<DeleteRecurringPaymentCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new DeleteRecurringPaymentCommand(id), cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}
