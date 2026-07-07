using Microsoft.Extensions.Localization;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Resources;

internal sealed class GetResources : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/resources", (IStringLocalizer<SharedResource> localizer) =>
        {
            Dictionary<string, string> resources = localizer
                .GetAllStrings(includeParentCultures: true)
                .ToDictionary(s => s.Name, s => s.Value, StringComparer.Ordinal);

            return Results.Ok(resources);
        }).CacheOutput("resources");
    }
}
