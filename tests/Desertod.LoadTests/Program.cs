using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.LoadTests;
using Microsoft.AspNetCore.SignalR.Client;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;

// Usage: dotnet run -c Release -- --base http://localhost:5095 --api-pid 1234 --minutes 3 [--only c250-v200]
// The API must run in Development: players sign in through /auth/dev-login.
var options = Options.Parse(args);
Phase[] phases =
[
    new("c025-v050", Villages: 1, CitizensPerVillage: 25, ViewersPerVillage: 50),
    new("c100-v050", Villages: 1, CitizensPerVillage: 100, ViewersPerVillage: 50),
    new("c250-v200", Villages: 1, CitizensPerVillage: 250, ViewersPerVillage: 200),
    new("spread-10x25", Villages: 10, CitizensPerVillage: 25, ViewersPerVillage: 10),
];

const string CarryToStorage = "carry-to-storage";
var runId = DateTime.Now.ToString("MMddHHmmss");
var summaries = new List<string>();
foreach (var phase in phases.Where(p => options.Only is null || p.Name == options.Only))
{
    summaries.Add(await RunPhaseAsync(phase));
}

Console.WriteLine();
Console.WriteLine("==== Summary ====");
summaries.ForEach(Console.WriteLine);
await File.WriteAllLinesAsync(Path.Combine(options.ReportFolder, $"summary-{runId}.txt"), summaries);
return;

async Task<string> RunPhaseAsync(Phase phase)
{
    Log($"phase {phase.Name}: preparing {phase.Villages} village(s), {phase.Villages * phase.CitizensPerVillage} citizens, {phase.Villages * phase.ViewersPerVillage} viewers");
    var stats = new BroadcastStats();
    await using var tsar = await Player.SignInAsync(options.BaseUri, $"tsar-{phase.Name}-{runId}", "");
    var slugs = new List<string>();
    for (var index = 0; index < phase.Villages; index++)
    {
        var created = await tsar.Http.PostAsJsonAsync("/api/villages",
            new { name = $"Load {phase.Name} {index} {runId}", description = (string?)null, isPrivate = false });
        created.EnsureSuccessStatusCode();
        slugs.Add((await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!);
    }

    using var gate = new SemaphoreSlim(options.SetupParallelism);
    var players = await Task.WhenAll(slugs
        .SelectMany((slug, village) => Enumerable.Range(0, phase.CitizensPerVillage).Select(index => (slug, village, index)))
        .Select(async item =>
        {
            await gate.WaitAsync();
            try
            {
                var player = await Player.SignInAsync(options.BaseUri, $"p{runId}-{phase.Name}-{item.village}-{item.index}", item.slug);
                var started = Stopwatch.GetTimestamp();
                await player.JoinAsync($"Load {item.index}");
                stats.Joins.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                await player.WatchAsync(options.BaseUri, stats);
                return player;
            }
            finally
            {
                gate.Release();
            }
        }));

    // Viewers are signed-in accounts without a character; the village feed refuses anonymous connections.
    var viewers = await Task.WhenAll(slugs
        .SelectMany((slug, village) => Enumerable.Range(0, phase.ViewersPerVillage).Select(index => (slug, village, index)))
        .Select(async item =>
        {
            await gate.WaitAsync();
            try
            {
                var viewer = await Player.SignInAsync(options.BaseUri, $"v{runId}-{phase.Name}-{item.village}-{item.index}", item.slug);
                await viewer.WatchAsync(options.BaseUri, stats);
                return viewer;
            }
            finally
            {
                gate.Release();
            }
        }));

    Log($"phase {phase.Name}: running for {options.Duration.TotalMinutes} min. Watch it at {options.BaseUri}v/{slugs[0]}");
    var scenario = Scenario.Create("player", context => PlayAsync(players[context.ScenarioInfo.InstanceNumber % players.Length], context))
        .WithoutWarmUp()
        .WithLoadSimulations(Simulation.KeepConstant(players.Length, options.Duration));

    var stopwatch = Stopwatch.StartNew();
    var sampler = new ResourceSampler(options.ApiProcessId, options.DatabaseContainer);
    NBomberRunner.RegisterScenarios(scenario)
        .WithTestSuite("desertod")
        .WithTestName(phase.Name)
        .WithReportFolder(Path.Combine(options.ReportFolder, $"{runId}-{phase.Name}"))
        .WithReportFormats(ReportFormat.Html, ReportFormat.Md)
        .Run();
    stopwatch.Stop();
    await sampler.DisposeAsync();

    var summary = string.Join(Environment.NewLine,
        $"[{phase.Name}] {players.Length} citizens in {phase.Villages} village(s), {viewers.Length} extra viewers, {stopwatch.Elapsed.TotalSeconds:F0} s",
        stats.Summary(stopwatch.Elapsed, players.Length + viewers.Length),
        sampler.Summary());
    Log(summary);

    await Task.WhenAll(players.Select(p => p.DisposeAsync().AsTask()).Concat(viewers.Select(v => v.DisposeAsync().AsTask())));
    return summary;
}

async Task<IResponse> PlayAsync(Player player, IScenarioContext context)
{
    var path = $"/api/villages/{player.Slug}";
    var me = await Step.Run("me", context, async () =>
    {
        using var response = await player.Http.GetAsync($"{path}/me");
        var body = await response.Content.ReadAsByteArrayAsync();
        var code = ((int)response.StatusCode).ToString();
        return response.IsSuccessStatusCode
            ? Response.Ok(payload: JsonDocument.Parse(body).RootElement.Clone(), statusCode: code, sizeBytes: body.Length)
            : Response.Fail<JsonElement>(statusCode: code, message: ErrorCode(body), sizeBytes: body.Length);
    });

    if (!me.IsError)
    {
        var citizen = me.Payload.Value;
        if (citizen.GetProperty("state").GetString() != "Working")
        {
            if (citizen.GetProperty("load").GetArrayLength() > 0)
            {
                // Like the game client: carry the goods to storage; throw them away only when storage is full.
                // A failed step ends the iteration in NBomber, so the full-storage answer counts as an expected outcome.
                var carried = await Step.Run("carry", context, () =>
                    SendAsync(() => player.Http.PostAsJsonAsync($"{path}/me/task", new { taskId = CarryToStorage }), expected: "hands.noRoom"));
                if (carried.StatusCode == "hands.noRoom")
                {
                    await Step.Run("drop", context, () => SendAsync(() => player.Http.PostAsync($"{path}/me/drop", null)));
                }
                else
                {
                    player.MarkAction();
                }
            }
            else
            {
                var taskId = options.Tasks[Random.Shared.Next(options.Tasks.Length)];
                var started = await Step.Run("start-task", context, () => SendAsync(() => player.Http.PostAsJsonAsync($"{path}/me/task", new { taskId })));
                if (!started.IsError)
                {
                    player.MarkAction();
                }
            }
        }
    }

    if (Random.Shared.NextDouble() < options.VillageReloadShare)
    {
        await Step.Run("village", context, () => SendAsync(() => player.Http.GetAsync(path)));
    }

    await Task.Delay(Random.Shared.Next(2000, 4000), context.ScenarioCancellationToken);
    return Response.Ok();
}

/// <param name="expected">A game rule answer that is a normal outcome of this step, reported as success with that status.</param>
static async Task<Response<object>> SendAsync(Func<Task<HttpResponseMessage>> send, string? expected = null)
{
    using var response = await send();
    var body = await response.Content.ReadAsByteArrayAsync();
    var code = ((int)response.StatusCode).ToString();
    if (response.IsSuccessStatusCode)
    {
        return Response.Ok(statusCode: code, sizeBytes: body.Length);
    }

    var error = ErrorCode(body);
    return error == expected
        ? Response.Ok(statusCode: error, sizeBytes: body.Length)
        : Response.Fail(statusCode: code, message: error, sizeBytes: body.Length);
}

static string ErrorCode(byte[] body)
{
    try
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() ?? "" : "";
    }
    catch (JsonException)
    {
        return "";
    }
}

static void Log(string message) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");

internal sealed record Phase(string Name, int Villages, int CitizensPerVillage, int ViewersPerVillage);

internal sealed record Options(
    Uri BaseUri,
    int? ApiProcessId,
    string? DatabaseContainer,
    TimeSpan Duration,
    string? Only,
    string ReportFolder)
{
    public string[] Tasks { get; } = ["chop-wood", "tend-fire", "study-plants", "roam"];
    public double VillageReloadShare => 0.1;
    public int SetupParallelism => 32;

    public static Options Parse(string[] args)
    {
        string? Value(string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        var reports = Value("--reports") ?? Path.Combine(AppContext.BaseDirectory, "reports");
        Directory.CreateDirectory(reports);
        return new Options(
            new Uri(Value("--base") ?? "http://localhost:5095/"),
            Value("--api-pid") is { } pid ? int.Parse(pid) : null,
            Value("--db-container") ?? "desertod-postgres",
            TimeSpan.FromMinutes(double.Parse(Value("--minutes") ?? "3", System.Globalization.CultureInfo.InvariantCulture)),
            Value("--only"),
            reports);
    }
}
