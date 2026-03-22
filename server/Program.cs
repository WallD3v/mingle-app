using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Mingle.Server.Auth;
using Mingle.Server.Contracts;
using Mingle.Server.Data;
using Mingle.Server.Services;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration["DATABASE_URL"]
    ?? throw new InvalidOperationException("DATABASE_URL is required");
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? "dev_jwt_secret_change_me";
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? "mingle-server";
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? "mingle-client";
var jwtExpiryDays = int.TryParse(builder.Configuration["JWT_EXPIRY_DAYS"], out var parsedExpiryDays)
    ? parsedExpiryDays
    : 30;

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(new AppJwtOptions(jwtSecret, jwtIssuer, jwtAudience, jwtExpiryDays));
builder.Services.AddSingleton<MnemonicService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<AuthService>();

builder.Services.AddSingleton(_ =>
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    return dataSourceBuilder.Build();
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
    await DbInitializer.InitializeAsync(dataSource);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/auth/register", async (AuthRequest request, AuthService authService) =>
{
    try
    {
        var result = await authService.RegisterAsync(request.Mnemonic);
        return Results.Ok(result);
    }
    catch (InvalidMnemonicException)
    {
        return Results.BadRequest(new ErrorResponse("INVALID_MNEMONIC", "Mnemonic phrase is invalid."));
    }
    catch (Exception)
    {
        return Results.Problem(title: "Server error", statusCode: 500,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "SERVER_ERROR",
                ["message"] = "Unexpected error."
            });
    }
});

app.MapPost("/auth/login", async (AuthRequest request, AuthService authService) =>
{
    try
    {
        var result = await authService.LoginAsync(request.Mnemonic);
        return Results.Ok(result);
    }
    catch (InvalidMnemonicException)
    {
        return Results.BadRequest(new ErrorResponse("INVALID_MNEMONIC", "Mnemonic phrase is invalid."));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Json(new ErrorResponse("UNAUTHORIZED", "Account not found."), statusCode: 401);
    }
    catch (Exception)
    {
        return Results.Problem(title: "Server error", statusCode: 500,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "SERVER_ERROR",
                ["message"] = "Unexpected error."
            });
    }
});

app.MapGet("/me", (ClaimsPrincipal user) =>
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(sub))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new MeResponse(sub));
    })
    .RequireAuthorization();

app.Run();

public partial class Program;
