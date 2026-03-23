namespace Mingle.Server.Data;

public sealed class DialogListItemRecord
{
    public Guid DialogId { get; set; }
    public Guid PeerUserId { get; set; }
    public string PeerDisplayName { get; set; } = string.Empty;
    public string PeerUsername { get; set; } = string.Empty;
    public DateTime PeerLastSeenAt { get; set; }
    public string LastMessageText { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
}

public sealed class DialogMessageRecord
{
    public Guid MessageId { get; set; }
    public Guid DialogId { get; set; }
    public Guid SenderUserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadByRecipientAt { get; set; }
}

public sealed class DialogThreadRecord
{
    public Guid? DialogId { get; set; }
    public Guid PeerUserId { get; set; }
    public string PeerDisplayName { get; set; } = string.Empty;
    public string PeerUsername { get; set; } = string.Empty;
    public DateTime PeerLastSeenAt { get; set; }
    public IReadOnlyList<DialogMessageRecord> Messages { get; set; } = Array.Empty<DialogMessageRecord>();
    public bool HasMoreBefore { get; set; }
    public long OldestLoadedUnixMs { get; set; }
    public long ReadUpdatedAtUnixMs { get; set; }
}
