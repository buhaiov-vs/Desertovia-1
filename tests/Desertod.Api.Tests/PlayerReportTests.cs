using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Director;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class PlayerReportTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);
    private Task<DesertodDbContext> Db() => factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private PlayerReportRequest Request(string? slug, bool image = true) => new(Guid.NewGuid(), "bug", "Дверь не открывается", slug,
        factory.Clock.GetUtcNow(), new("village", "menu:build", "Hut #2", null, 2, 3, 390, 844, "0.7.14", "Android", 30, 100, 0),
        image ? "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDGooorxD5E/9k=" : null);
    private PlayerReportService Service(Bridge bridge, IConfiguration? config = null) => new(
        factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>(), bridge,
        config ?? new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Hosting:ReleaseSha"]="tested-server" }).Build(),
        factory.Clock, NullLogger<PlayerReportService>.Instance);
    private sealed class Bridge : IAlmightyBridge
    {
        public List<PlayerReportNotice> Notices { get; } = [];
        public string State { get; set; } = "Sent";
        public Task<AlmightyAnswer?> AnalyzeAsync(AlmightyReview review,CancellationToken ct)=>Task.FromResult<AlmightyAnswer?>(null);
        public Task<AlmightyDelivery> NotifyAsync(AlmightyNotice notice,CancellationToken ct)=>Task.FromResult(new AlmightyDelivery("Unavailable"));
        public Task<SramoslavReply?> SpeakAsync(object request,CancellationToken ct)=>Task.FromResult<SramoslavReply?>(null);
        public Task<AlmightyDelivery> NotifyReportAsync(PlayerReportNotice notice,CancellationToken ct)
        { Notices.Add(notice);return Task.FromResult(new AlmightyDelivery(State)); }
    }

    [Fact]
    public async Task Anonymous_cannot_submit_and_opening_the_game_creates_no_report()
    {
        await using var db=await Db();var before=await db.PlayerReports.CountAsync();
        using var client=factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/reports",Request(null))).StatusCode);
        Assert.Equal(before,await db.PlayerReports.CountAsync());
    }

    [Fact]
    public async Task Authenticated_author_and_context_are_saved_once_without_changing_progress()
    {
        var village=await players.FoundVillageAsync("Report identity");var citizen=await players.JoinAsync(village,"Report citizen");
        await players.ArrangeCitizenAsync(citizen.Id,(c,_)=>{c.Renown=82;c.StoredEnergy=14;});
        var request=Request(village.Slug);
        var body=JsonSerializer.SerializeToNode(request,PlayerReportServiceOptions())!;
        body["accountId"]=Guid.NewGuid().ToString();body["telegramId"]="999";body["cookies"]="secret-not-accepted";
        var receipt=await OkJsonAsync(await citizen.Client.PostAsJsonAsync("/api/reports",body));
        Assert.Equal(request.Id,receipt.GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.OK,(await citizen.Client.PostAsJsonAsync("/api/reports",request)).StatusCode);
        await using var db=await Db();var report=await db.PlayerReports.SingleAsync(r=>r.Id==request.Id);
        var actual=await db.Citizens.AsNoTracking().SingleAsync(c=>c.Id==citizen.Id);
        Assert.Equal(actual.AccountId,report.AccountId);Assert.Equal(82,actual.Renown);Assert.Equal(14,actual.StoredEnergy);
        Assert.DoesNotContain("secret-not-accepted",report.Payload);Assert.DoesNotContain("999",report.Payload);
        var payload=JsonDocument.Parse(report.Payload).RootElement;
        Assert.Equal(citizen.Id,payload.GetProperty("author").GetProperty("citizen").GetProperty("id").GetGuid());
        Assert.Equal("menu:build",payload.GetProperty("screen").GetProperty("menu").GetString());
        Assert.Equal(1,await db.PlayerReports.CountAsync(r=>r.Id==request.Id));
        Assert.NotNull(report.Screenshot);
    }
    private static JsonSerializerOptions PlayerReportServiceOptions()=>new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Changed_retry_and_other_authors_cannot_reuse_an_existing_report()
    {
        var village=await players.FoundVillageAsync("Report dedup");var citizen=await players.JoinAsync(village);
        var request=Request(village.Slug);await OkJsonAsync(await citizen.Client.PostAsJsonAsync("/api/reports",request));
        await AssertRejectedAsync(await citizen.Client.PostAsJsonAsync("/api/reports",request with {Text="Changed"}),HttpStatusCode.BadRequest,"report.changed");
        Assert.Equal(HttpStatusCode.Forbidden,(await village.Tsar.PostAsJsonAsync("/api/reports",request)).StatusCode);
    }

    [Fact]
    public async Task Private_village_is_not_accessible_to_outside_reporters()
    {
        var village=await players.FoundVillageAsync("Report private",true);var outsider=await players.WithCharacterAsync("Outsider");
        Assert.Equal(HttpStatusCode.Forbidden,(await outsider.Client.PostAsJsonAsync("/api/reports",Request(village.Slug))).StatusCode);
    }

    [Fact]
    public async Task Invalid_fields_images_and_more_than_five_reports_are_rejected()
    {
        var village=await players.FoundVillageAsync("Report validation");var request=Request(village.Slug);
        foreach(var bad in new[]{request with {Kind="other"},request with {Text=" "},request with {Text=new string('x',2001)},
            request with {Screenshot="file:///secret"},request with {Screenshot=Convert.ToBase64String(new byte[]{255,216,255,217})},request with {Screenshot=Convert.ToBase64String(new byte[350001])},
            request with {CapturedAt=factory.Clock.GetUtcNow().AddDays(1)}})
            await AssertRejectedAsync(await village.Tsar.PostAsJsonAsync("/api/reports",bad),HttpStatusCode.BadRequest,"report.invalid");
        for(var i=0;i<5;i++)await OkJsonAsync(await village.Tsar.PostAsJsonAsync("/api/reports",Request(village.Slug,false)));
        await AssertRejectedAsync(await village.Tsar.PostAsJsonAsync("/api/reports",Request(village.Slug)),HttpStatusCode.BadRequest,"report.limit");
    }

    [Fact]
    public async Task Full_attachment_budget_still_saves_text_and_clearly_reports_missing_image()
    {
        var village=await players.FoundVillageAsync("Report storage cap");
        Guid actor;await using(var db=await Db())actor=await db.Villages.Where(v=>v.Slug==village.Slug).Select(v=>v.OwnerAccountId).SingleAsync();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Game:ReportScreenshotStorageBytes"]="0"}).Build();
        var request=Request(village.Slug);var service=Service(new Bridge(),config);
        var receipt=await service.SubmitAsync(actor,request,default);Assert.False(receipt.ScreenshotIncluded);
        var retry=await service.SubmitAsync(actor,request,default);Assert.Equal(receipt,retry);
        await using(var db=await Db()){
            var saved=await db.PlayerReports.SingleAsync(r=>r.Id==request.Id);Assert.Null(saved.Screenshot);
            Assert.Contains("screenshotOmittedForStorageLimit",saved.Payload);Assert.Equal(request.Text,saved.Text);
        }
    }

    [Fact]
    public async Task Durable_delivery_retries_same_id_and_expires_only_delivered_images()
    {
        var village=await players.FoundVillageAsync("Report delivery");var bridge=new Bridge{State="Unavailable"};var service=Service(bridge);
        Guid actor;await using(var db=await Db()){
            actor=await db.Villages.Where(v=>v.Slug==village.Slug).Select(v=>v.OwnerAccountId).SingleAsync();
            db.ExternalLogins.Add(new ExternalLogin{Provider="telegram",ProviderUserId="770715",AccountId=actor});await db.SaveChangesAsync();
            // Other tests leave outboxes; isolate this worker tick without advancing their reports.
            await db.PlayerReports.Where(r=>r.AccountId!=actor).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.NextAttemptAt,factory.Clock.GetUtcNow().AddDays(30)));
        }
        var request=Request(village.Slug);await service.SubmitAsync(actor,request,default);
        await service.TickAsync(default);Assert.Single(bridge.Notices);Assert.Equal("770715",bridge.Notices[0].OwnerId);
        await using(var db=await Db()){
            var saved=await db.PlayerReports.SingleAsync(r=>r.Id==request.Id);Assert.Equal("Unavailable",saved.DeliveryState);
            saved.NextAttemptAt=factory.Clock.GetUtcNow();await db.SaveChangesAsync();
        }
        bridge.State="Sent";await service.TickAsync(default);Assert.Equal(2,bridge.Notices.Count);Assert.Equal(bridge.Notices[0],bridge.Notices[1]);
        await using(var db=await Db()){
            var saved=await db.PlayerReports.SingleAsync(r=>r.Id==request.Id);saved.CreatedAt=factory.Clock.GetUtcNow().AddDays(-8);await db.SaveChangesAsync();
        }
        await service.TickAsync(default);
        await using(var db=await Db())Assert.Null((await db.PlayerReports.SingleAsync(r=>r.Id==request.Id)).Screenshot);
        Assert.Equal(2,bridge.Notices.Count);
    }
}
