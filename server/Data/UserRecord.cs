namespace Mingle.Server.Data;

public sealed record UserRecord(Guid Id, string AccountKey, DateTimeOffset CreatedAt);
