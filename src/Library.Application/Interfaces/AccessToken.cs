namespace Library.Application.Interfaces;

/// <summary>
/// A signed access token and the UTC time at which it stops being accepted.
/// </summary>
public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);
