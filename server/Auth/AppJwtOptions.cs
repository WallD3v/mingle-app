namespace Mingle.Server.Auth;

public sealed record AppJwtOptions(string Secret, string Issuer, string Audience, int ExpiryDays);
