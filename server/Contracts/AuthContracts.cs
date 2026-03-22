namespace Mingle.Server.Contracts;

public sealed record AuthRequest(string Mnemonic);
public sealed record AuthResponse(string AccessToken, string UserId);
public sealed record ErrorResponse(string Code, string Message);
public sealed record MeResponse(string UserId);
