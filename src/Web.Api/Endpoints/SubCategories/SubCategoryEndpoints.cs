using Application.Abstractions.Messaging;
using Application.SubCategories;
using Application.SubCategories.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.SubCategories;

internal sealed class CreateSubCategory : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/subcategories", async (
            CreateSubCategoryCommand command,
            ICommandHandler<CreateSubCategoryCommand, Guid> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(
                localizer,
                id => Results.Created($"/subcategories/{id}", new { id }));
        }).RequireAuthorization();
    }
}

internal sealed class GetSubCategories : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/subcategories", async (
            string? category,
            IQueryHandler<GetSubCategoriesQuery, List<SubCategoryResponse>> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<List<SubCategoryResponse>> result = await handler.Handle(
                new GetSubCategoriesQuery(category), cancellationToken);

            return result.ToHttpResult(localizer, Results.Ok);
        }).RequireAuthorization();
    }
}

internal sealed class DeleteSubCategory : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/subcategories/{id:guid}", async (
            Guid id,
            ICommandHandler<DeleteSubCategoryCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new DeleteSubCategoryCommand(id), cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}
