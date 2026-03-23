using Mingle.Server.Protocol;
using Mingle.Server.Data;
using ProtoBuf;

namespace Mingle.Server.Services;

public sealed class TcpMessageProcessor(
    AuthService authService,
    JwtValidationService jwtValidationService,
    IUserRepository userRepository,
    IDialogRepository dialogRepository,
    IRealtimeConnectionRegistry realtimeRegistry,
    ILogger<TcpMessageProcessor> logger)
{
    public const uint ProtocolVersion = 1;

    public async Task<ServerMessage> ProcessAsync(ClientMessage message, Guid? connectionId = null, CancellationToken cancellationToken = default)
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
                if (connectionId.HasValue)
                {
                    var heartbeat = realtimeRegistry.MarkHeartbeat(
                        connectionId.Value,
                        message.Ping.IsAppForeground,
                        DateTime.UtcNow);

                    if (heartbeat.Success && heartbeat.PresenceChanged && heartbeat.UserId.HasValue)
                    {
                        if (!heartbeat.IsOnline)
                        {
                            await userRepository.TouchLastSeenAsync(heartbeat.UserId.Value);
                        }

                        await realtimeRegistry.BroadcastPresenceAsync(
                            heartbeat.UserId.Value,
                            heartbeat.IsOnline,
                            heartbeat.LastSeenAtUnixMs,
                            cancellationToken);
                    }
                }

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

            if (message.DialogsList is not null)
            {
                var dialogsUserId = jwtValidationService.ValidateAndGetUserId(message.DialogsList.Token);
                if (!Guid.TryParse(dialogsUserId, out var userId))
                {
                    return Error("UNAUTHORIZED", "Token is invalid.");
                }

                var dialogs = await dialogRepository.GetDialogsAsync(userId, 100);
                return new ServerMessage
                {
                    ProtocolVersion = ProtocolVersion,
                    DialogsData = new DialogsData
                    {
                        Items = dialogs.Select(ToDialogListItem).ToList()
                    }
                };
            }

            if (message.DialogOpen is not null)
            {
                var dialogUserId = jwtValidationService.ValidateAndGetUserId(message.DialogOpen.Token);
                if (!Guid.TryParse(dialogUserId, out var userId))
                {
                    return Error("UNAUTHORIZED", "Token is invalid.");
                }

                if (!Guid.TryParse(message.DialogOpen.PeerUserId, out var peerUserId))
                {
                    return Error("USER_NOT_FOUND", "Peer user not found.");
                }

                var requestedLimit = message.DialogOpen.Limit == 0 ? 40 : (int)message.DialogOpen.Limit;
                long? beforeUnixMs = message.DialogOpen.BeforeUnixMs > 0 ? message.DialogOpen.BeforeUnixMs : null;
                var dialog = await dialogRepository.GetDialogAsync(userId, peerUserId, requestedLimit, beforeUnixMs);
                if (dialog is null)
                {
                    return Error("USER_NOT_FOUND", "Peer user not found.");
                }

                if (dialog.ReadUpdatedAtUnixMs > 0)
                {
                    await realtimeRegistry.PushToUserAsync(
                        peerUserId,
                        new ServerMessage
                        {
                            ProtocolVersion = ProtocolVersion,
                            MessageReadUpdate = new MessageReadUpdate
                            {
                                DialogId = dialog.DialogId?.ToString() ?? string.Empty,
                                ReaderUserId = userId.ToString(),
                                ReadAtUnixMs = dialog.ReadUpdatedAtUnixMs
                            }
                        },
                        cancellationToken);
                }

                return new ServerMessage
                {
                    ProtocolVersion = ProtocolVersion,
                    DialogData = ToDialogData(dialog)
                };
            }

            if (message.MessageSend is not null)
            {
                var senderUserId = jwtValidationService.ValidateAndGetUserId(message.MessageSend.Token);
                if (!Guid.TryParse(senderUserId, out var userId))
                {
                    return Error("UNAUTHORIZED", "Token is invalid.");
                }

                if (!Guid.TryParse(message.MessageSend.PeerUserId, out var peerUserId))
                {
                    return Error("USER_NOT_FOUND", "Peer user not found.");
                }

                if (string.IsNullOrWhiteSpace(message.MessageSend.Text))
                {
                    return Error("INVALID_MESSAGE", "Message text cannot be empty.");
                }

                var sent = await dialogRepository.SendMessageAsync(userId, peerUserId, message.MessageSend.Text, 200);
                if (sent is null)
                {
                    return Error("USER_NOT_FOUND", "Peer user not found.");
                }

                var sender = await userRepository.GetByUserIdAsync(userId);
                if (sender is not null)
                {
                    await realtimeRegistry.PushMessageReceivedAsync(
                        peerUserId,
                        new MessageReceived
                        {
                            Message = ToDialogMessage(sent.Value.Message),
                            From = ToUserPreview(sender)
                        },
                        cancellationToken);
                }

                return new ServerMessage
                {
                    ProtocolVersion = ProtocolVersion,
                    MessageSent = new MessageSent
                    {
                        Message = ToDialogMessage(sent.Value.Message),
                        Peer = ToUserPreview(sent.Value.Thread)
                    }
                };
            }

            if (message.SubscribeUpdates is not null)
            {
                var subscribedUserId = jwtValidationService.ValidateAndGetUserId(message.SubscribeUpdates.Token);
                if (!Guid.TryParse(subscribedUserId, out var userId))
                {
                    return Error("UNAUTHORIZED", "Token is invalid.");
                }

                if (!connectionId.HasValue)
                {
                    return Error("SERVER_ERROR", "Connection subscribe failed.");
                }

                var subscription = realtimeRegistry.Subscribe(connectionId.Value, userId);
                if (!subscription.Success)
                {
                    return Error("SERVER_ERROR", "Connection subscribe failed.");
                }

                if (subscription.PresenceChanged)
                {
                    if (!subscription.IsOnline && subscription.UserId.HasValue)
                    {
                        await userRepository.TouchLastSeenAsync(subscription.UserId.Value);
                    }

                    await realtimeRegistry.BroadcastPresenceAsync(
                        userId,
                        isOnline: subscription.IsOnline,
                        lastSeenAtUnixMs: subscription.LastSeenAtUnixMs,
                        cancellationToken);
                }

                return new ServerMessage
                {
                    ProtocolVersion = ProtocolVersion,
                    Subscribed = new Subscribed { UserId = userId.ToString() }
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

    private ProfileData ToProfileData(UserRecord profile)
    {
        return new ProfileData
        {
            UserId = profile.Id.ToString(),
            DisplayName = profile.DisplayName,
            Username = profile.Username,
            LastSeenAtUnixMs = new DateTimeOffset(profile.LastSeenAt).ToUnixTimeMilliseconds(),
            IsOnline = realtimeRegistry.IsUserOnline(profile.Id)
        };
    }

    private UserSearchResultItem ToSearchItem(UserRecord user)
    {
        return new UserSearchResultItem
        {
            UserId = user.Id.ToString(),
            DisplayName = user.DisplayName,
            Username = user.Username,
            LastSeenAtUnixMs = new DateTimeOffset(user.LastSeenAt).ToUnixTimeMilliseconds(),
            IsOnline = realtimeRegistry.IsUserOnline(user.Id)
        };
    }

    private DialogListItem ToDialogListItem(DialogListItemRecord item)
    {
        return new DialogListItem
        {
            DialogId = item.DialogId.ToString(),
            Peer = new UserPreview
            {
                UserId = item.PeerUserId.ToString(),
                DisplayName = item.PeerDisplayName,
                Username = item.PeerUsername,
                LastSeenAtUnixMs = new DateTimeOffset(item.PeerLastSeenAt).ToUnixTimeMilliseconds(),
                IsOnline = realtimeRegistry.IsUserOnline(item.PeerUserId)
            },
            LastMessageText = item.LastMessageText,
            LastMessageAtUnixMs = new DateTimeOffset(item.LastMessageAt).ToUnixTimeMilliseconds(),
            UnreadCount = (uint)Math.Max(0, item.UnreadCount)
        };
    }

    private DialogData ToDialogData(DialogThreadRecord thread)
    {
        return new DialogData
        {
            DialogId = thread.DialogId?.ToString() ?? string.Empty,
            Peer = ToUserPreview(thread),
            Messages = thread.Messages.Select(ToDialogMessage).ToList(),
            HasMoreBefore = thread.HasMoreBefore,
            OldestLoadedUnixMs = thread.OldestLoadedUnixMs
        };
    }

    private static DialogMessage ToDialogMessage(DialogMessageRecord message)
    {
        return new DialogMessage
        {
            MessageId = message.MessageId.ToString(),
            DialogId = message.DialogId.ToString(),
            SenderUserId = message.SenderUserId.ToString(),
            Text = message.Text,
            CreatedAtUnixMs = new DateTimeOffset(message.CreatedAt).ToUnixTimeMilliseconds(),
            ReadByRecipientAtUnixMs = message.ReadByRecipientAt.HasValue
                ? new DateTimeOffset(message.ReadByRecipientAt.Value).ToUnixTimeMilliseconds()
                : 0
        };
    }

    private UserPreview ToUserPreview(DialogThreadRecord thread)
    {
        return new UserPreview
        {
            UserId = thread.PeerUserId.ToString(),
            DisplayName = thread.PeerDisplayName,
            Username = thread.PeerUsername,
            LastSeenAtUnixMs = new DateTimeOffset(thread.PeerLastSeenAt).ToUnixTimeMilliseconds(),
            IsOnline = realtimeRegistry.IsUserOnline(thread.PeerUserId)
        };
    }

    private UserPreview ToUserPreview(UserRecord user)
    {
        return new UserPreview
        {
            UserId = user.Id.ToString(),
            DisplayName = user.DisplayName,
            Username = user.Username,
            LastSeenAtUnixMs = new DateTimeOffset(user.LastSeenAt).ToUnixTimeMilliseconds(),
            IsOnline = realtimeRegistry.IsUserOnline(user.Id)
        };
    }
}
