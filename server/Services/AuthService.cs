using Mingle.Server.Contracts;
using Mingle.Server.Data;

namespace Mingle.Server.Services;

public sealed class AuthService(
    MnemonicService mnemonicService,
    IUserRepository userRepository,
    JwtTokenService jwtTokenService)
{
    public async Task<AuthResponse> RegisterAsync(string mnemonic)
    {
        mnemonicService.EnsureValid(mnemonic);
        var accountKey = mnemonicService.ComputeAccountKey(mnemonic);

        var user = await userRepository.UpsertByAccountKeyAsync(accountKey);
        var token = jwtTokenService.CreateAccessToken(user.Id);

        return new AuthResponse(token, user.Id.ToString());
    }

    public async Task<AuthResponse> LoginAsync(string mnemonic)
    {
        mnemonicService.EnsureValid(mnemonic);
        var accountKey = mnemonicService.ComputeAccountKey(mnemonic);

        var user = await userRepository.GetByAccountKeyAsync(accountKey);
        if (user is null)
        {
            throw new UnauthorizedAccessException();
        }

        var token = jwtTokenService.CreateAccessToken(user.Id);
        return new AuthResponse(token, user.Id.ToString());
    }
}
