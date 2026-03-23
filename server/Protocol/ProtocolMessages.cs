using ProtoBuf;

namespace Mingle.Server.Protocol;

[ProtoContract]
public sealed class ClientMessage
{
    [ProtoMember(1)] public uint ProtocolVersion { get; set; } = 1;
    [ProtoMember(2)] public AuthRequest? Register { get; set; }
    [ProtoMember(3)] public AuthRequest? Login { get; set; }
    [ProtoMember(4)] public MeRequest? Me { get; set; }
    [ProtoMember(5)] public PingRequest? Ping { get; set; }
    [ProtoMember(6)] public ProfileGetRequest? ProfileGet { get; set; }
    [ProtoMember(7)] public ProfileUpdateRequest? ProfileUpdate { get; set; }
    [ProtoMember(8)] public UserSearchRequest? UserSearch { get; set; }
    [ProtoMember(9)] public DialogsListRequest? DialogsList { get; set; }
    [ProtoMember(10)] public DialogOpenRequest? DialogOpen { get; set; }
    [ProtoMember(11)] public MessageSendRequest? MessageSend { get; set; }
    [ProtoMember(12)] public SubscribeUpdatesRequest? SubscribeUpdates { get; set; }
}

[ProtoContract]
public sealed class ServerMessage
{
    [ProtoMember(1)] public uint ProtocolVersion { get; set; } = 1;
    [ProtoMember(2)] public AuthSuccess? AuthSuccess { get; set; }
    [ProtoMember(3)] public MeSuccess? MeSuccess { get; set; }
    [ProtoMember(4)] public ErrorResponse? Error { get; set; }
    [ProtoMember(5)] public PongResponse? Pong { get; set; }
    [ProtoMember(6)] public ProfileData? ProfileData { get; set; }
    [ProtoMember(7)] public ProfileData? ProfileUpdated { get; set; }
    [ProtoMember(8)] public UserSearchResults? UserSearchResults { get; set; }
    [ProtoMember(9)] public DialogsData? DialogsData { get; set; }
    [ProtoMember(10)] public DialogData? DialogData { get; set; }
    [ProtoMember(11)] public MessageSent? MessageSent { get; set; }
    [ProtoMember(12)] public Subscribed? Subscribed { get; set; }
    [ProtoMember(13)] public MessageReceived? MessageReceived { get; set; }
    [ProtoMember(14)] public MessageReadUpdate? MessageReadUpdate { get; set; }
    [ProtoMember(15)] public PresenceUpdate? PresenceUpdate { get; set; }
}

[ProtoContract]
public sealed class AuthRequest
{
    [ProtoMember(1)] public string Mnemonic { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class MeRequest
{
    [ProtoMember(1)] public string Token { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class PingRequest
{
    [ProtoMember(1)] public long UnixTimeMs { get; set; }
}

[ProtoContract]
public sealed class ProfileGetRequest
{
    [ProtoMember(1)] public string Token { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class ProfileUpdateRequest
{
    [ProtoMember(1)] public string Token { get; set; } = string.Empty;
    [ProtoMember(2)] public string DisplayName { get; set; } = string.Empty;
    [ProtoMember(3)] public string Username { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class AuthSuccess
{
    [ProtoMember(1)] public string AccessToken { get; set; } = string.Empty;
    [ProtoMember(2)] public string UserId { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class MeSuccess
{
    [ProtoMember(1)] public string UserId { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class ErrorResponse
{
    [ProtoMember(1)] public string Code { get; set; } = string.Empty;
    [ProtoMember(2)] public string Message { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class PongResponse
{
    [ProtoMember(1)] public long UnixTimeMs { get; set; }
}

[ProtoContract]
public sealed class ProfileData
{
    [ProtoMember(1)] public string UserId { get; set; } = string.Empty;
    [ProtoMember(2)] public string DisplayName { get; set; } = string.Empty;
    [ProtoMember(3)] public string Username { get; set; } = string.Empty;
    [ProtoMember(4)] public long LastSeenAtUnixMs { get; set; }
    [ProtoMember(5)] public bool IsOnline { get; set; }
}

[ProtoContract]
public sealed class UserSearchRequest
{
    [ProtoMember(1)] public string Token { get; set; } = string.Empty;
    [ProtoMember(2)] public string Query { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class UserSearchResultItem
{
    [ProtoMember(1)] public string UserId { get; set; } = string.Empty;
    [ProtoMember(2)] public string DisplayName { get; set; } = string.Empty;
    [ProtoMember(3)] public string Username { get; set; } = string.Empty;
    [ProtoMember(4)] public long LastSeenAtUnixMs { get; set; }
    [ProtoMember(5)] public bool IsOnline { get; set; }
}

[ProtoContract]
public sealed class UserSearchResults
{
    [ProtoMember(1)] public List<UserSearchResultItem> Items { get; set; } = new();
}

[ProtoContract]
public sealed class DialogsListRequest
{
    [ProtoMember(1)] public string Token { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class DialogOpenRequest
{
    [ProtoMember(1)] public string Token { get; set; } = string.Empty;
    [ProtoMember(2)] public string PeerUserId { get; set; } = string.Empty;
    [ProtoMember(3)] public long BeforeUnixMs { get; set; }
    [ProtoMember(4)] public uint Limit { get; set; }
}

[ProtoContract]
public sealed class MessageSendRequest
{
    [ProtoMember(1)] public string Token { get; set; } = string.Empty;
    [ProtoMember(2)] public string PeerUserId { get; set; } = string.Empty;
    [ProtoMember(3)] public string Text { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class UserPreview
{
    [ProtoMember(1)] public string UserId { get; set; } = string.Empty;
    [ProtoMember(2)] public string DisplayName { get; set; } = string.Empty;
    [ProtoMember(3)] public string Username { get; set; } = string.Empty;
    [ProtoMember(4)] public long LastSeenAtUnixMs { get; set; }
    [ProtoMember(5)] public bool IsOnline { get; set; }
}

[ProtoContract]
public sealed class DialogMessage
{
    [ProtoMember(1)] public string MessageId { get; set; } = string.Empty;
    [ProtoMember(2)] public string DialogId { get; set; } = string.Empty;
    [ProtoMember(3)] public string SenderUserId { get; set; } = string.Empty;
    [ProtoMember(4)] public string Text { get; set; } = string.Empty;
    [ProtoMember(5)] public long CreatedAtUnixMs { get; set; }
    [ProtoMember(6)] public long ReadByRecipientAtUnixMs { get; set; }
}

[ProtoContract]
public sealed class DialogListItem
{
    [ProtoMember(1)] public string DialogId { get; set; } = string.Empty;
    [ProtoMember(2)] public UserPreview? Peer { get; set; }
    [ProtoMember(3)] public string LastMessageText { get; set; } = string.Empty;
    [ProtoMember(4)] public long LastMessageAtUnixMs { get; set; }
    [ProtoMember(5)] public uint UnreadCount { get; set; }
}

[ProtoContract]
public sealed class DialogsData
{
    [ProtoMember(1)] public List<DialogListItem> Items { get; set; } = new();
}

[ProtoContract]
public sealed class DialogData
{
    [ProtoMember(1)] public string DialogId { get; set; } = string.Empty;
    [ProtoMember(2)] public UserPreview? Peer { get; set; }
    [ProtoMember(3)] public List<DialogMessage> Messages { get; set; } = new();
    [ProtoMember(4)] public bool HasMoreBefore { get; set; }
    [ProtoMember(5)] public long OldestLoadedUnixMs { get; set; }
}

[ProtoContract]
public sealed class MessageSent
{
    [ProtoMember(1)] public DialogMessage? Message { get; set; }
    [ProtoMember(2)] public UserPreview? Peer { get; set; }
}

[ProtoContract]
public sealed class SubscribeUpdatesRequest
{
    [ProtoMember(1)] public string Token { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class Subscribed
{
    [ProtoMember(1)] public string UserId { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class MessageReceived
{
    [ProtoMember(1)] public DialogMessage? Message { get; set; }
    [ProtoMember(2)] public UserPreview? From { get; set; }
}

[ProtoContract]
public sealed class MessageReadUpdate
{
    [ProtoMember(1)] public string DialogId { get; set; } = string.Empty;
    [ProtoMember(2)] public string ReaderUserId { get; set; } = string.Empty;
    [ProtoMember(3)] public long ReadAtUnixMs { get; set; }
}

[ProtoContract]
public sealed class PresenceUpdate
{
    [ProtoMember(1)] public string UserId { get; set; } = string.Empty;
    [ProtoMember(2)] public bool IsOnline { get; set; }
    [ProtoMember(3)] public long LastSeenAtUnixMs { get; set; }
}
