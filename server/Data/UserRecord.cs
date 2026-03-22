namespace Mingle.Server.Data;

public sealed class UserRecord
{
    public Guid Id { get; set; }
    public string AccountKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
