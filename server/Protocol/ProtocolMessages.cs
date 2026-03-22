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
}

[ProtoContract]
public sealed class UserSearchResults
{
    [ProtoMember(1)] public List<UserSearchResultItem> Items { get; set; } = new();
}
