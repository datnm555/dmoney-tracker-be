using SharedKernel;

namespace Domain.Users;

/// <summary>
/// Long-lived opaque token that lets a client mint a new access token without
/// re-entering credentials. Rotated on every refresh (the used row is replaced).
/// </summary>
public sealed class RefreshToken : AuditedEntity
{
    public const int LifetimeDays = 30;

    public const int TokenMaxLength = 128;

    private RefreshToken() { }

    public Guid UserId { get; private set; }

    /// <summary>Opaque random value handed to the client.</summary>
    public string Token { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;

    public static RefreshToken Create(Guid userId, string token, DateTime utcNow) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        Token = token,
        ExpiresAt = utcNow.AddDays(LifetimeDays)
    };
}
