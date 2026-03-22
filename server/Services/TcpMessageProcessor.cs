using Mingle.Server.Protocol;
using ProtoBuf;

namespace Mingle.Server.Services;

public sealed class TcpMessageProcessor(
    AuthService authService,
    JwtValidationService jwtValidationService)
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

            return Error("SERVER_ERROR", "Empty payload.");
        }
        catch (InvalidMnemonicException)
        {
            return Error("INVALID_MNEMONIC", "Mnemonic phrase is invalid.");
        }
        catch (UnauthorizedAccessException)
        {
            return Error("UNAUTHORIZED", "Account not found.");
        }
        catch
        {
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
}
