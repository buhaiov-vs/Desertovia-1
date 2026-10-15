using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Chat;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class ChaplainChatTests(DesertodApiFactory factory)
{
    [Theory]
    [InlineData("Батюшка, помоги",true)]
    [InlineData("@sramoslav_bot привет!",true)]
    [InlineData("Отче, как дела?",true)]
    [InlineData("Срамославушка",false)]
    [InlineData("я работаю",false)]
    public void Calls_are_explicit_and_lines_fit_the_bubble(string text,bool expected)
    {
        Assert.Equal(expected,ChaplainChatWorker.IsCalled(text));
        Assert.True(ChaplainChatWorker.ShortLine(new string('а',200)).Length<=120);
    }

    [Fact]
    public async Task Unavailable_ai_is_silent_after_reconnect_and_obeys_durable_budget()
    {
        var players = new Players(factory);
        var village = await players.FoundVillageAsync("Chaplain Village");
        var services = factory.Services;
        var contexts = services.GetRequiredService<IDbContextFactory<DesertodDbContext>>();
        var clock = factory.Clock;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["Hosting:PrimaryVillage"]=village.Slug, ["Game:ChaplainChat:Enabled"]="true" }).Build();
        ChaplainChatWorker NewWorker() => new(contexts,services.GetRequiredService<SramoslavService>(),
            services.GetRequiredService<CitizenPresence>(),services.GetRequiredService<VillageNotifier>(),clock,config,
            NullLogger<ChaplainChatWorker>.Instance);
        using var worker = NewWorker();
        var path = village.Path+"/chat";
        (await village.Tsar.PostAsJsonAsync(path,new{text="Батюшка, привет!"})).EnsureSuccessStatusCode();
        await worker.TickAsync(default);
        var history = await village.Tsar.GetFromJsonAsync<JsonElement>(path);
        var bots=history.EnumerateArray().Where(m=>m.GetProperty("isChaplain").GetBoolean()).ToList();
        Assert.Empty(bots);
        using var outsider=(await players.WithCharacterAsync("Outsider")).Client;
        Assert.Equal(HttpStatusCode.Forbidden,(await outsider.GetAsync(path)).StatusCode);
        (await village.Tsar.PostAsJsonAsync(path,new{text="Батюшка, ещё вопрос"})).EnsureSuccessStatusCode();
        using var restarted=NewWorker(); await restarted.TickAsync(default);
        Assert.DoesNotContain((await village.Tsar.GetFromJsonAsync<JsonElement>(path)).EnumerateArray(),m=>m.GetProperty("isChaplain").GetBoolean());
        // A durable full hourly budget prevents even a newly started worker from writing.
        clock.Advance(TimeSpan.FromSeconds(100));
        await using var db=await contexts.CreateDbContextAsync();
        var villageId=await db.Villages.Where(v=>v.Slug==village.Slug).Select(v=>v.Id).SingleAsync();
        db.ChaplainMessages.AddRange(Enumerable.Range(0,12).Select(i=>ChaplainMessage.Speak(villageId,"Чадо, держимся вместе.",clock.GetUtcNow().AddMinutes(-3))));
        await db.SaveChangesAsync();
        using var capped=NewWorker();
        (await village.Tsar.PostAsJsonAsync(path,new{text="Батюшка, ответишь?"})).EnsureSuccessStatusCode();
        await capped.TickAsync(default);
        Assert.Equal(12,await db.ChaplainMessages.CountAsync(m=>m.VillageId==villageId));
    }
}
