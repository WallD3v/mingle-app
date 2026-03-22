using System.IdentityModel.Tokens.Jwt;
using Mingle.Server.Auth;
using Mingle.Server.Data;
using Mingle.Server.Services;
using Xunit;

namespace Mingle.Server.Tests;

public sealed class AuthFlowTests
{
    private const string ValidMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon art";
    private const string TestJwtSecret = "test_secret_very_long_1234567890_abcdef";

    [Fact]
    public async Task Register_IsIdempotent_ForSameMnemonic()
    {
        var mnemonicService = new MnemonicService();
        var repository = new InMemoryUserRepository();
        var tokenService = new JwtTokenService(new AppJwtOptions(TestJwtSecret, "issuer", "aud", 30));
        var authService = new AuthService(mnemonicService, repository, tokenService);

        var first = await authService.RegisterAsync(ValidMnemonic);
        var second = await authService.RegisterAsync(ValidMnemonic);

        Assert.Equal(first.UserId, second.UserId);
        Assert.NotEqual(string.Empty, first.AccessToken);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenMnemonicUnknown()
    {
        var mnemonicService = new MnemonicService();
        var repository = new InMemoryUserRepository();
        var tokenService = new JwtTokenService(new AppJwtOptions(TestJwtSecret, "issuer", "aud", 30));
        var authService = new AuthService(mnemonicService, repository, tokenService);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authService.LoginAsync(ValidMnemonic));
    }

    [Fact]
    public void MnemonicValidation_RejectsInvalidChecksum()
    {
        var service = new MnemonicService();
        var invalid = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon invalidword";

        Assert.Throws<InvalidMnemonicException>(() => service.EnsureValid(invalid));
    }

    [Fact]
    public void JwtToken_ContainsSubClaim()
    {
        var userId = Guid.NewGuid();
        var tokenService = new JwtTokenService(new AppJwtOptions(TestJwtSecret, "issuer", "aud", 30));
        var token = tokenService.CreateAccessToken(userId);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;

        Assert.Equal(userId.ToString(), sub);
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
            var results = _users.Values
                .Where(u => u.Id != requesterUserId && u.Username.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(u => u.Username)
                .Take(Math.Clamp(limit, 1, 50))
                .ToList();
            return Task.FromResult<IReadOnlyList<UserRecord>>(results);
        }
    }
}
