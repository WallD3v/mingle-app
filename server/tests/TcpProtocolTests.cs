using Mingle.Server.Auth;
using Mingle.Server.Data;
using Mingle.Server.Protocol;
using Mingle.Server.Services;
using Mingle.Server.Transport;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public async Task Processor_ProfileGet_ReturnsProfileData()
    {
        var processor = BuildProcessor();

        var register = await processor.ProcessAsync(new ClientMessage
        {
            ProtocolVersion = 1,
            Register = new AuthRequest { Mnemonic = ValidMnemonic }
        });

        var profile = await processor.ProcessAsync(new ClientMessage
        {
            ProtocolVersion = 1,
            ProfileGet = new ProfileGetRequest { Token = register.AuthSuccess!.AccessToken }
        });

        Assert.NotNull(profile.ProfileData);
        Assert.Equal("WallDev", profile.ProfileData!.DisplayName);
    }

    [Fact]
    public async Task Processor_ProfileUpdate_UsernameTaken_ReturnsError()
    {
        var processor = BuildProcessor();
        var secondMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon ability able";

        var first = await processor.ProcessAsync(new ClientMessage
        {
            ProtocolVersion = 1,
            Register = new AuthRequest { Mnemonic = ValidMnemonic }
        });

        var second = await processor.ProcessAsync(new ClientMessage
        {
            ProtocolVersion = 1,
            Register = new AuthRequest { Mnemonic = secondMnemonic }
        });

        var updateOne = await processor.ProcessAsync(new ClientMessage
        {
            ProtocolVersion = 1,
            ProfileUpdate = new ProfileUpdateRequest
            {
                Token = first.AuthSuccess!.AccessToken,
                DisplayName = "User One",
                Username = "walldev123"
            }
        });
        Assert.NotNull(updateOne.ProfileUpdated);

        var updateTwo = await processor.ProcessAsync(new ClientMessage
        {
            ProtocolVersion = 1,
            ProfileUpdate = new ProfileUpdateRequest
            {
                Token = second.AuthSuccess!.AccessToken,
                DisplayName = "User Two",
                Username = "WallDev123"
            }
        });

        Assert.NotNull(updateTwo.Error);
        Assert.Equal("USERNAME_TAKEN", updateTwo.Error!.Code);
    }

    [Fact]
    public async Task Processor_UserSearch_ReturnsMatchingUsernames()
    {
        var processor = BuildProcessor();
        var secondMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon ability able";

        var first = await processor.ProcessAsync(new ClientMessage
        {
            ProtocolVersion = 1,
            Register = new AuthRequest { Mnemonic = ValidMnemonic }
        });

        var second = await processor.ProcessAsync(new ClientMessage
        {
            ProtocolVersion = 1,
            Register = new AuthRequest { Mnemonic = secondMnemonic }
        });

        await processor.ProcessAsync(new ClientMessage
        {
            ProtocolVersion = 1,
            ProfileUpdate = new ProfileUpdateRequest
            {
                Token = second.AuthSuccess!.AccessToken,
                DisplayName = "Second",
                Username = "searchme1"
            }
        });

        var search = await processor.ProcessAsync(new ClientMessage
        {
            ProtocolVersion = 1,
            UserSearch = new UserSearchRequest
            {
                Token = first.AuthSuccess!.AccessToken,
                Query = "search"
            }
        });

        Assert.NotNull(search.UserSearchResults);
        Assert.Single(search.UserSearchResults!.Items);
        Assert.Equal("searchme1", search.UserSearchResults.Items[0].Username);
    }

    private static TcpMessageProcessor BuildProcessor()
    {
        var mnemonicService = new MnemonicService();
        var repo = new InMemoryUserRepository();
        var jwtOptions = new AppJwtOptions(TestJwtSecret, "issuer", "aud", 30);
        var jwtTokenService = new JwtTokenService(jwtOptions);
        var authService = new AuthService(mnemonicService, repo, jwtTokenService);
        var validationService = new JwtValidationService(jwtOptions);
        return new TcpMessageProcessor(authService, validationService, repo, NullLogger<TcpMessageProcessor>.Instance);
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

            var created = new UserRecord
            {
                Id = Guid.NewGuid(),
                AccountKey = accountKey,
                DisplayName = "WallDev",
                Username = $"user_{accountKey[..8]}",
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            };
            _users[accountKey] = created;
            return Task.FromResult(created);
        }

        public Task<UserRecord?> GetByAccountKeyAsync(string accountKey)
        {
            _users.TryGetValue(accountKey, out var user);
            return Task.FromResult(user);
        }

        public Task<UserRecord?> GetByUserIdAsync(Guid userId)
        {
            var user = _users.Values.FirstOrDefault(x => x.Id == userId);
            return Task.FromResult(user);
        }

        public Task<UserRecord?> UpdateProfileAsync(Guid userId, string displayName, string username)
        {
            if (_users.Values.Any(u => u.Id != userId && string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult<UserRecord?>(null);
            }

            var user = _users.Values.FirstOrDefault(x => x.Id == userId);
            if (user is null)
            {
                return Task.FromResult<UserRecord?>(null);
            }

            user.DisplayName = displayName;
            user.Username = username;
            return Task.FromResult<UserRecord?>(user);
        }

        public Task TouchLastSeenAsync(Guid userId)
        {
            var user = _users.Values.FirstOrDefault(x => x.Id == userId);
            if (user is not null)
            {
                user.LastSeenAt = DateTime.UtcNow;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UserRecord>> SearchByUsernameAsync(Guid requesterUserId, string query, int limit)
        {
            var normalized = query.Trim();
            if (normalized.Length == 0)
            {
                return Task.FromResult<IReadOnlyList<UserRecord>>(Array.Empty<UserRecord>());
            }

            var results = _users.Values
                .Where(u => u.Id != requesterUserId && u.Username.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                .OrderBy(u => u.Username)
                .Take(Math.Clamp(limit, 1, 50))
                .ToList();

            return Task.FromResult<IReadOnlyList<UserRecord>>(results);
        }
    }
}
