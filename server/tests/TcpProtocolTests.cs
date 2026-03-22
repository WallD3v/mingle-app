using Mingle.Server.Auth;
using Mingle.Server.Data;
using Mingle.Server.Protocol;
using Mingle.Server.Services;
using Mingle.Server.Transport;
using Xunit;

namespace Mingle.Server.Tests;

public sealed class TcpProtocolTests
{
    private const string ValidMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon art";
    private const string TestJwtSecret = "test_secret_very_long_1234567890_abcdef";

    [Fact]
    public async Task FrameCodec_RoundTrip_Works()
    {
        var stream = new MemoryStream();
        var payload = new byte[] { 1, 2, 3, 4, 5 };

        await TcpFrameCodec.WriteFrameAsync(stream, payload, CancellationToken.None);
        stream.Position = 0;

        var actual = await TcpFrameCodec.ReadFrameAsync(stream, CancellationToken.None);
        Assert.NotNull(actual);
        Assert.Equal(payload, actual);
    }

    [Fact]
    public async Task FrameCodec_PartialPayload_Throws()
    {
        var stream = new MemoryStream();
        await stream.WriteAsync(new byte[] { 0, 0, 0, 10 });
        await stream.WriteAsync(new byte[] { 1, 2, 3 });
        stream.Position = 0;

        await Assert.ThrowsAsync<EndOfStreamException>(() => TcpFrameCodec.ReadFrameAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task FrameCodec_OversizedFrame_Throws()
    {
        var stream = new MemoryStream();
        await stream.WriteAsync(new byte[] { 0x7F, 0xFF, 0xFF, 0x7F });
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(() => TcpFrameCodec.ReadFrameAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task Processor_Register_Then_Me_ReturnsUser()
    {
        var processor = BuildProcessor();

        var register = await processor.ProcessAsync(new ClientMessage
        {
            ProtocolVersion = 1,
            Register = new AuthRequest { Mnemonic = ValidMnemonic }
        });

        Assert.NotNull(register.AuthSuccess);

        var me = await processor.ProcessAsync(new ClientMessage
        {
            ProtocolVersion = 1,
            Me = new MeRequest { Token = register.AuthSuccess!.AccessToken }
        });

        Assert.NotNull(me.MeSuccess);
        Assert.Equal(register.AuthSuccess.UserId, me.MeSuccess!.UserId);
    }

    [Fact]
    public async Task Processor_InvalidMnemonic_ReturnsError()
    {
        var processor = BuildProcessor();
        var response = await processor.ProcessAsync(new ClientMessage
        {
            ProtocolVersion = 1,
            Login = new AuthRequest { Mnemonic = "invalid words" }
        });

        Assert.NotNull(response.Error);
        Assert.Equal("INVALID_MNEMONIC", response.Error!.Code);
    }

    [Fact]
    public async Task Processor_UnknownAccount_ReturnsUnauthorized()
    {
        var processor = BuildProcessor();

        var response = await processor.ProcessAsync(new ClientMessage
        {
            ProtocolVersion = 1,
            Login = new AuthRequest { Mnemonic = ValidMnemonic }
        });

        Assert.NotNull(response.Error);
        Assert.Equal("UNAUTHORIZED", response.Error!.Code);
    }

    private static TcpMessageProcessor BuildProcessor()
    {
        var mnemonicService = new MnemonicService();
        var repo = new InMemoryUserRepository();
        var jwtOptions = new AppJwtOptions(TestJwtSecret, "issuer", "aud", 30);
        var jwtTokenService = new JwtTokenService(jwtOptions);
        var authService = new AuthService(mnemonicService, repo, jwtTokenService);
        var validationService = new JwtValidationService(jwtOptions);
        return new TcpMessageProcessor(authService, validationService);
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly Dictionary<string, UserRecord> _users = new();

        public Task<UserRecord> UpsertByAccountKeyAsync(string accountKey)
        {
            if (_users.TryGetValue(accountKey, out var existing))
            {
                return Task.FromResult(existing);
            }

            var created = new UserRecord(Guid.NewGuid(), accountKey, DateTimeOffset.UtcNow);
            _users[accountKey] = created;
            return Task.FromResult(created);
        }

        public Task<UserRecord?> GetByAccountKeyAsync(string accountKey)
        {
            _users.TryGetValue(accountKey, out var user);
            return Task.FromResult(user);
        }
    }
}
