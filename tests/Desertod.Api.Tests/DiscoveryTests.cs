using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Domain.Catalog;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Storage;
using Desertod.Domain.Technologies;
using Desertod.Domain.Weather;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

/// <summary>Use case: the village votes on its next discovery, researches it and puts it to use.</summary>
[Collection(ApiCollection.Name)]
public sealed class DiscoveryTests(DesertodApiFactory factory)
{
    private static readonly string[] CampChoices = [TechnologyCatalog.StoneTools, TechnologyCatalog.HerbLore];

    private readonly Players players = new(factory);

    private static Task<HttpResponseMessage> OpenPollAsync(HttpClient ruler, FoundedVillage village, params string[] technologyIds) =>
        ruler.PostAsJsonAsync($"{village.Path}/poll", new { technologyIds });

    private static Task<HttpResponseMessage> VoteAsync(Resident citizen, string technologyId) =>
        citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/vote", new { technologyId });

    [Fact]
    public async Task Village_chooses_its_next_discovery_by_vote()
    {
        var village = await players.FoundVillageAsync("Voting Village");
        var ann = await players.JoinAsync(village, "Ann");
        var bob = await players.JoinAsync(village, "Bob");
        var carl = await players.JoinAsync(village, "Carl");

        var opened = await OkJsonAsync(await OpenPollAsync(village.Tsar, village, CampChoices));
        var poll = opened.GetProperty("poll");
        Assert.Equal(2, poll.GetProperty("options").GetArrayLength());
        Assert.Equal(2, poll.GetProperty("votesNeeded").GetInt32());

        await AssertRuleBrokenAsync(await VoteAsync(ann, TechnologyCatalog.Weaving), "poll.notAnOption");
        await OkJsonAsync(await VoteAsync(ann, TechnologyCatalog.HerbLore));
        var changedMind = await OkJsonAsync(await VoteAsync(ann, TechnologyCatalog.StoneTools));
        var ballots = changedMind.GetProperty("poll").GetProperty("ballots").EnumerateArray().ToList();
        Assert.Equal(TechnologyCatalog.StoneTools, Assert.Single(ballots).GetProperty("technologyId").GetString());
        Assert.Equal(JsonValueKind.Null, changedMind.GetProperty("poll").GetProperty("winner").ValueKind);

        var closed = await OkJsonAsync(await VoteAsync(bob, TechnologyCatalog.StoneTools));

        Assert.Equal(TechnologyCatalog.StoneTools, closed.GetProperty("poll").GetProperty("winner").GetString());
        Assert.Equal(TechnologyCatalog.StoneTools, closed.GetProperty("currentResearch").GetString());
        await AssertRuleBrokenAsync(await VoteAsync(carl, TechnologyCatalog.HerbLore), "poll.notOpen");
    }

    [Fact]
    public async Task Citizens_away_for_a_while_still_live_in_the_village_but_do_not_hold_the_vote_open()
    {
        var village = await players.FoundVillageAsync("Absent Voters");
        var ann = await players.JoinAsync(village, "Ann");
        await players.JoinAsync(village, "Bob");
        await players.JoinAsync(village, "Carl");
        var opened = await OkJsonAsync(await OpenPollAsync(village.Tsar, village, CampChoices));
        Assert.Equal(3, opened.GetProperty("poll").GetProperty("electorate").GetInt32());

        factory.Clock.Advance(GameRules.PresenceWindow + TimeSpan.FromSeconds(1));

        var seen = await OkJsonAsync(await village.Tsar.GetAsync(village.Path));
        Assert.Equal(3, seen.GetProperty("citizens").GetArrayLength());
        Assert.Equal(3, seen.GetProperty("awayCitizenIds").GetArrayLength());
        Assert.Equal(0, seen.GetProperty("state").GetProperty("poll").GetProperty("electorate").GetInt32());

        var closed = await OkJsonAsync(await VoteAsync(ann, TechnologyCatalog.HerbLore));

        Assert.Equal(TechnologyCatalog.HerbLore, closed.GetProperty("poll").GetProperty("winner").GetString());
        Assert.Equal(1, closed.GetProperty("poll").GetProperty("electorate").GetInt32());
        var after = await OkJsonAsync(await village.Tsar.GetAsync(village.Path));
        Assert.DoesNotContain(after.GetProperty("awayCitizenIds").EnumerateArray(), id => id.GetGuid() == ann.Id);
    }

    [Fact]
    public async Task Vote_needs_available_discoveries_and_citizens()
    {
        var village = await players.FoundVillageAsync("Empty Polls");

        await AssertRuleBrokenAsync(await OpenPollAsync(village.Tsar, village, CampChoices), "poll.noCitizens");
        await players.JoinAsync(village);
        await AssertRuleBrokenAsync(await OpenPollAsync(village.Tsar, village, TechnologyCatalog.HerbLore), "poll.tooFewOptions");
        await AssertRuleBrokenAsync(
            await OpenPollAsync(village.Tsar, village, TechnologyCatalog.HerbLore, TechnologyCatalog.Agriculture), "technology.requires");
        await AssertRuleBrokenAsync(await OpenPollAsync(village.Tsar, village, "time-travel", TechnologyCatalog.HerbLore), "technology.unknown");
    }

    [Fact]
    public async Task Ruler_withdraws_an_open_vote()
    {
        var village = await players.FoundVillageAsync("Withdrawn Vote");
        var citizen = await players.JoinAsync(village);
        await AssertRuleBrokenAsync(await village.Tsar.DeleteAsync($"{village.Path}/poll"), "poll.notOpen");
        await OkJsonAsync(await OpenPollAsync(village.Tsar, village, CampChoices));
        await AssertRuleBrokenAsync(await OpenPollAsync(village.Tsar, village, CampChoices), "poll.alreadyOpen");

        var cancelled = await OkJsonAsync(await village.Tsar.DeleteAsync($"{village.Path}/poll"));

        var poll = cancelled.GetProperty("poll");
        Assert.Equal(JsonValueKind.String, poll.GetProperty("closedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, poll.GetProperty("winner").ValueKind);
        await AssertRuleBrokenAsync(await VoteAsync(citizen, TechnologyCatalog.StoneTools), "poll.notOpen");
    }

    [Fact]
    public async Task Citizens_cannot_call_a_vote_unless_appointed_Governor()
    {
        var village = await players.FoundVillageAsync("Governed Vote");
        var citizen = await players.JoinAsync(village);

        await AssertForbiddenAsync(await OpenPollAsync(citizen.Client, village, CampChoices));
        await AssertForbiddenAsync(await citizen.Client.DeleteAsync($"{village.Path}/poll"));

        await OkJsonAsync(await village.Tsar.PutAsJsonAsync($"{village.Path}/citizens/{citizen.Id}/governor", new { isGovernor = true }));
        await OkJsonAsync(await OpenPollAsync(citizen.Client, village, CampChoices));
        await OkJsonAsync(await citizen.Client.DeleteAsync($"{village.Path}/poll"));
    }

    [Fact]
    public async Task Citizen_researches_the_chosen_discovery_at_its_building()
    {
        var village = await players.FoundVillageAsync("Research Village");
        var citizen = await players.JoinAsync(village);
        var research = $"{citizen.Path}/me/research";
        await AssertRuleBrokenAsync(await citizen.Client.PostAsync(research, null), "research.none");

        await OkJsonAsync(await OpenPollAsync(village.Tsar, village, CampChoices));
        await OkJsonAsync(await VoteAsync(citizen, TechnologyCatalog.StoneTools));
        await AssertRuleBrokenAsync(await citizen.Client.PostAsync(research, null), "research.needsBuilding");

        await players.ArrangeVillageAsync(village.Slug, (v, now) => v.Storages.Add(StorageBuilding.Create(StorageKind.StonePile, "21,17", now)));
        await AssertRuleBrokenAsync(await citizen.Client.PostAsync(research, null), "research.skillRequired");

        await players.ArrangeCitizenAsync(citizen.Id, (c, _) => c.Stats.Strength = 2);
        var started = await OkJsonAsync(await citizen.Client.PostAsync(research, null));

        Assert.Equal("Working", started.GetProperty("state").GetString());
        Assert.Equal(TechnologyCatalog.StoneTools, started.GetProperty("technologyId").GetString());
        Assert.Equal(TechnologyCatalog.ResearchTaskId, started.GetProperty("taskId").GetString());
    }

    [Fact]
    public async Task Synoptic_forecasts_the_weather_of_the_next_period()
    {
        var village = await players.FoundVillageAsync("Weather Village");
        var citizen = await players.JoinAsync(village, "Synoptic");
        var forecast = $"{citizen.Path}/me/forecast";
        await AssertRuleBrokenAsync(await citizen.Client.PostAsJsonAsync(forecast, new { rain = true }), "forecast.needsTechnology");

        await players.ArrangeVillageAsync(village.Slug, (v, now) =>
        {
            v.Research.Add(new TechnologyProgress { TechnologyId = TechnologyCatalog.Agriculture, ResearchedAt = now });
            v.Research.Add(new TechnologyProgress { TechnologyId = TechnologyCatalog.WeatherLore, ResearchedAt = now });
        });
        await AssertRuleBrokenAsync(await citizen.Client.PostAsJsonAsync(forecast, new { rain = true }), "task.roleRequired");

        var synoptic = GameCatalog.FindRole(WeatherRules.SynopticRole)!;
        await players.ArrangeCitizenAsync(citizen.Id, (c, _) =>
            c.Skills.Add(new SkillProgress { Kind = synoptic.Skill!.Value, Level = synoptic.SkillLevel }));
        var made = await OkJsonAsync(await citizen.Client.PostAsJsonAsync(forecast, new { rain = true }));

        var next = made.GetProperty("weather").GetProperty("next");
        Assert.True(next.GetProperty("saysRain").GetBoolean());
        Assert.Equal(citizen.Id, next.GetProperty("citizenId").GetGuid());
        await AssertRuleBrokenAsync(await citizen.Client.PostAsJsonAsync(forecast, new { rain = false }), "forecast.alreadyMade");
    }
}
