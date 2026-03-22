using Mingle.Server.Auth;
using Mingle.Server.Data;
using Mingle.Server.Services;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration["DATABASE_URL"]
    ?? throw new InvalidOperationException("DATABASE_URL is required");
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? "dev_jwt_secret_change_me_32_bytes_minimum";
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
builder.Services.AddSingleton<JwtValidationService>();
builder.Services.AddSingleton<TcpMessageProcessor>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<AuthService>();

builder.Services.AddHostedService<TcpServerHostedService>();

builder.Services.AddSingleton(_ =>
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    return dataSourceBuilder.Build();
});

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

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
