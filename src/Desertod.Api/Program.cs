using System.IO.Compression;
using System.Text.Json.Serialization;
using Desertod.Api;
using Desertod.Api.Auth;
using Desertod.Api.Game;
using Desertod.Domain.Combat;
using Desertod.Domain.Weather;
using Desertod.Infrastructure;
using Desertod.Infrastructure.Data;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;

if (args.Contains(HealthProbe.Argument))
{
    return await HealthProbe.RunAsync();
}

var builder = WebApplication.CreateBuilder(args);

// Added last so it overrides appsettings and environment variables; edits apply to the running process.
var logLevelsFile = builder.Configuration["LogLevelsFile"];
if (!string.IsNullOrWhiteSpace(logLevelsFile))
{
    builder.Configuration.AddJsonFile(Path.GetFullPath(logLevelsFile), optional: true, reloadOnChange: true);
}

var gameOptions = builder.Configuration.GetSection(GameOptions.Section).Get<GameOptions>() ?? new GameOptions();
DayCycle.Configure(gameOptions.DayLength);
CombatRules.Configure(gameOptions.PeacefulTime);
builder.Services.AddSingleton(DtoMapper.Catalog(gameOptions));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Desertod")
    ?? throw new InvalidOperationException("Connection string 'Desertod' is not configured."));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Plain HTTP only (the default): behind the planned HTTPS reverse proxy, and without the BREACH risk of compressing
// HTTPS responses that carry invite codes next to player-chosen names.
builder.Services.AddResponseCompression(options =>
{
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

builder.Services.AddDesertodDataProtection(builder.Configuration, builder.Environment);
builder.Services.AddDesertodAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GameExceptionHandler>();
builder.Services.AddHealthChecks().AddDbContextCheck<DesertodDbContext>();

builder.Services.AddSingleton<TelegramLoginVerifier>();
builder.Services.AddSingleton<VillageNotifier>();
builder.Services.AddSingleton<VillageStateChanges>();
builder.Services.AddSingleton<CitizenPresence>();
builder.Services.AddSingleton<GameService>();
builder.Services.AddSingleton<ChatService>();
builder.Services.AddSingleton<DungeonRuntime>();
builder.Services.AddHostedService<CycleSettlementWorker>();
builder.Services.AddHostedService<RaidWorker>();
builder.Services.AddHostedService<PresenceWorker>();
builder.Services.AddHostedService<DungeonWorker>();
builder.Services.AddHostedService<VillageStatePublisher>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DesertodDbContext>();
    await db.Database.MigrateAsync();
    if (app.Environment.IsDevelopment())
    {
        await DemoVillage.SeedAsync(db, app.Services.GetRequiredService<TimeProvider>(), CancellationToken.None);
        await BuiltVillage.SeedAsync(db, app.Services.GetRequiredService<TimeProvider>(), CancellationToken.None);
    }
}

app.UseResponseCompression();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAccountLogScope();
app.UseAuthorization();
app.UseRateLimiter();

app.MapStaticAssets();
// Only for the in-container probe; requests through Caddy carry the public host name.
app.MapHealthChecks(HealthProbe.Path).RequireHost("localhost");
app.MapAuthEndpoints();
app.MapGameEndpoints();
app.MapHub<VillageHub>("/hubs/village");
app.MapHub<DungeonHub>("/hubs/dungeon");
app.MapFallbackToFile("index.html");

app.Run();
return 0;
