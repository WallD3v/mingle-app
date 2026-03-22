using Mingle.Server.Protocol;
using Mingle.Server.Data;
using ProtoBuf;

namespace Mingle.Server.Services;

public sealed class TcpMessageProcessor(
    AuthService authService,
    JwtValidationService jwtValidationService,
    IUserRepository userRepository,
    ILogger<TcpMessageProcessor> logger)
{
    public const uint ProtocolVersion = 1;

    public async Task<ServerMessage> ProcessAsync(ClientMessage message)
    {
        if (message.ProtocolVersion != ProtocolVersion)
        {
            return Error("UNSUPPORTED_PROTOCOL", "Unsupported protocol version.");
        }

        try
        {
            if (message.Register is not null)
            {
                var auth = await authService.RegisterAsync(message.Register.Mnemonic);
                if (Guid.TryParse(auth.UserId, out var registeredUserId))
                {
                    await userRepository.TouchLastSeenAsync(registeredUserId);
                }

                return new ServerMessage
                {
                    ProtocolVersion = ProtocolVersion,
                    AuthSuccess = new AuthSuccess
                    {
                        AccessToken = auth.AccessToken,
                        UserId = auth.UserId
                    }
                };
            }

            if (message.Login is not null)
            {
                var auth = await authService.LoginAsync(message.Login.Mnemonic);
                if (Guid.TryParse(auth.UserId, out var loginUserId))
                {
                    await userRepository.TouchLastSeenAsync(loginUserId);
                }

                return new ServerMessage
                {
                    ProtocolVersion = ProtocolVersion,
                    AuthSuccess = new AuthSuccess
                    {
                        AccessToken = auth.AccessToken,
                        UserId = auth.UserId
                    }
                };
            }

            if (message.Me is not null)
            {
                var userId = jwtValidationService.ValidateAndGetUserId(message.Me.Token);
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Error("UNAUTHORIZED", "Token is invalid.");
                }

                if (Guid.TryParse(userId, out var meUserId))
                {
                    await userRepository.TouchLastSeenAsync(meUserId);
                }

                return new ServerMessage
                {
                    ProtocolVersion = ProtocolVersion,
                    MeSuccess = new MeSuccess { UserId = userId }
                };
            }

            if (message.Ping is not null)
            {
                return new ServerMessage
                {
                    ProtocolVersion = ProtocolVersion,
                    Pong = new PongResponse { UnixTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
                };
            }

            if (message.ProfileGet is not null)
            {
                var profileUserId = jwtValidationService.ValidateAndGetUserId(message.ProfileGet.Token);
                if (!Guid.TryParse(profileUserId, out var userId))
                {
                    return Error("UNAUTHORIZED", "Token is invalid.");
                }

                var profile = await userRepository.GetByUserIdAsync(userId);
                if (profile is null)
                {
                    return Error("UNAUTHORIZED", "Account not found.");
                }

                return new ServerMessage
                {
                    ProtocolVersion = ProtocolVersion,
                    ProfileData = ToProfileData(profile)
                };
            }

            if (message.ProfileUpdate is not null)
            {
                var profileUserId = jwtValidationService.ValidateAndGetUserId(message.ProfileUpdate.Token);
                if (!Guid.TryParse(profileUserId, out var userId))
                {
                    return Error("UNAUTHORIZED", "Token is invalid.");
                }

                if (!IsValidUsername(message.ProfileUpdate.Username))
                {
                    return Error("INVALID_USERNAME", "Username must be at least 5 ASCII alphanumeric chars.");
                }

                var normalizedUsername = message.ProfileUpdate.Username.ToLowerInvariant();
                var normalizedDisplayName = string.IsNullOrWhiteSpace(message.ProfileUpdate.DisplayName)
                    ? "WallDev"
                    : message.ProfileUpdate.DisplayName.Trim();

                var updated = await userRepository.UpdateProfileAsync(userId, normalizedDisplayName, normalizedUsername);
                if (updated is null)
                {
                    return Error("USERNAME_TAKEN", "Username is already taken.");
                }

                return new ServerMessage
                {
                    ProtocolVersion = ProtocolVersion,
                    ProfileUpdated = ToProfileData(updated)
                };
            }

            if (message.UserSearch is not null)
            {
                var searchUserId = jwtValidationService.ValidateAndGetUserId(message.UserSearch.Token);
                if (!Guid.TryParse(searchUserId, out var userId))
                {
                    return Error("UNAUTHORIZED", "Token is invalid.");
                }

                var query = message.UserSearch.Query.Trim();
                if (query.Length == 0)
                {
                    return new ServerMessage
                    {
                        ProtocolVersion = ProtocolVersion,
                        UserSearchResults = new UserSearchResults()
                    };
                }

                var results = await userRepository.SearchByUsernameAsync(userId, query, limit: 20);
                return new ServerMessage
                {
                    ProtocolVersion = ProtocolVersion,
                    UserSearchResults = new UserSearchResults
                    {
                        Items = results.Select(ToSearchItem).ToList()
                    }
                };
            }

            return Error("SERVER_ERROR", "Empty payload.");
        }
        catch (InvalidMnemonicException)
        {
            logger.LogInformation("Invalid mnemonic received in TCP request.");
            return Error("INVALID_MNEMONIC", "Mnemonic phrase is invalid.");
        }
        catch (UnauthorizedAccessException)
        {
            logger.LogInformation("Unauthorized TCP auth attempt.");
            return Error("UNAUTHORIZED", "Account not found.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception while processing TCP message.");
            return Error("SERVER_ERROR", "Unexpected error.");
        }
    }

    public static byte[] Serialize<T>(T message)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        return stream.ToArray();
    }

    public static T Deserialize<T>(byte[] payload)
    {
        using var stream = new MemoryStream(payload);
        return Serializer.Deserialize<T>(stream);
    }

    private static ServerMessage Error(string code, string message)
    {
        return new ServerMessage
        {
            ProtocolVersion = ProtocolVersion,
            Error = new ErrorResponse
            {
                Code = code,
                Message = message
            }
        };
    }

    private static bool IsValidUsername(string username)
    {
        return username.Length >= 5 && username.All(ch => ch <= 127 && char.IsLetterOrDigit(ch));
    }

    private static ProfileData ToProfileData(UserRecord profile)
    {
        return new ProfileData
        {
            UserId = profile.Id.ToString(),
            DisplayName = profile.DisplayName,
            Username = profile.Username,
            LastSeenAtUnixMs = new DateTimeOffset(profile.LastSeenAt).ToUnixTimeMilliseconds()
        };
    }

    private static UserSearchResultItem ToSearchItem(UserRecord user)
    {
        return new UserSearchResultItem
        {
            UserId = user.Id.ToString(),
            DisplayName = user.DisplayName,
            Username = user.Username,
            LastSeenAtUnixMs = new DateTimeOffset(user.LastSeenAt).ToUnixTimeMilliseconds()
        };
    }
}
