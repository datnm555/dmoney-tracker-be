namespace Application.Users.Data;

public sealed record LoginResponse(
    string Token,
    Guid UserId,
    string Email,
    string Username,
    string DisplayName,
    string RefreshToken);
