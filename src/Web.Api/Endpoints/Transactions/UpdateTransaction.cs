using Application.Abstractions.Messaging;
using Application.Transactions;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Transactions;

internal sealed class UpdateTransaction : IEndpoint
{
    internal sealed record UpdateTransactionRequest(
        DateOnly Date,
        string Content,
        decimal CreditAmount,
        decimal DebitAmount,
        string? Note,
        Guid? CategoryId,
        string? PaymentMethod = null,
        string? CardType = null,
        string? Bank = null,
        bool IsAdvance = false,
        IReadOnlyList<Guid>? AdvanceTransactionIds = null,
        bool IsPrepaid = false,
        DateOnly? PrepaidFrom = null,
        DateOnly? PrepaidTo = null,
        Guid? PrepaidTransactionId = null,
        Guid? SubCategoryId = null,
        Guid? ReimbursedByTransactionId = null);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/transactions/{id:guid}", async (
            Guid id,
            UpdateTransactionRequest request,
            ICommandHandler<UpdateTransactionCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateTransactionCommand(
                id,
                request.Date,
                request.Content,
                request.CreditAmount,
                request.DebitAmount,
                request.Note,
                request.CategoryId,
                request.PaymentMethod,
                request.CardType,
                request.Bank,
                request.IsAdvance,
                request.AdvanceTransactionIds,
                request.IsPrepaid,
                request.PrepaidFrom,
                request.PrepaidTo,
                request.PrepaidTransactionId,
                request.SubCategoryId,
                request.ReimbursedByTransactionId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}
