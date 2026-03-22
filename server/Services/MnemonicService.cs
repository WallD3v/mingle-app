using System.Security.Cryptography;
using System.Text;
using NBitcoin;

namespace Mingle.Server.Services;

public sealed class InvalidMnemonicException : Exception;

public sealed class MnemonicService
{
    public string Normalize(string mnemonic)
    {
        var normalized = string.Join(
            " ",
            mnemonic
                .Trim()
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        );

        return normalized;
    }

    public void EnsureValid(string mnemonic)
    {
        var normalized = Normalize(mnemonic);
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length != 24)
        {
            throw new InvalidMnemonicException();
        }

        try
        {
            _ = new Mnemonic(normalized, Wordlist.English);
        }
        catch
        {
            throw new InvalidMnemonicException();
        }
    }

    public string ComputeAccountKey(string mnemonic)
    {
        var normalized = Normalize(mnemonic);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
