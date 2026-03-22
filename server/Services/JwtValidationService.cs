using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Mingle.Server.Auth;

namespace Mingle.Server.Services;

public sealed class JwtValidationService(AppJwtOptions options)
{
    private readonly TokenValidationParameters _validationParameters = new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = options.Issuer,
        ValidAudience = options.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    public string? ValidateAndGetUserId(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(token, _validationParameters, out _);
            return principal.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        }
        catch
        {
            return null;
        }
    }
}
