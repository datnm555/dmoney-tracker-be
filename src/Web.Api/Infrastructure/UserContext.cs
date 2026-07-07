using System.Security.Claims;
using Application.Abstractions.Authentication;

namespace Web.Api.Infrastructure;

/// <summary>
/// Reads the current user id from the raw JWT "sub" claim. Requires
/// MapInboundClaims = false on the bearer options so the claim keeps its name.
/// </summary>
internal sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public Guid? UserId
    {
        get
        {
            string? sub = httpContextAccessor.HttpContext?.User.FindFirstValue("sub");
            return Guid.TryParse(sub, out Guid userId) ? userId : null;
        }
    }
}
