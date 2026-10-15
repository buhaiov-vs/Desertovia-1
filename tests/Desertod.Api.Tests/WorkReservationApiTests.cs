using System.Net.Http.Json;
using Desertod.Api.Game;
using Desertod.Domain;
using Desertod.Domain.Buildings;
using Desertod.Domain.Rules;
using Desertod.Domain.Entities;
using Desertod.Domain.Economy;
using Desertod.Domain.Catalog;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class WorkReservationApiTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);

    private async Task<FoundedVillage> Prepare()
    {
        var village = await players.FoundVillageAsync("Reserved work");
        await players.ArrangeVillageAsync(village.Slug, (v, now) => {
            v.Trees.Clear();
            var site = ConstructionRules.Place(v, BuildingKind.WoodPile, 9, 11, now);
            site.Record(0, "bring-sticks", site.Required(0, "bring-sticks"), Guid.NewGuid());
            site.Record(0, "stack-pile", site.Required(0, "stack-pile") - 1, Guid.NewGuid());
        });
        return village;
    }

    [Fact]
    public async Task Last_door_finishes_once_with_other_nested_sites_and_other_workers_keep_their_progress()
    {
        var v=await players.FoundVillageAsync("Finish nested sites");
        var door=await players.JoinAsync(v,"Door worker");var haul=await players.JoinAsync(v,"Carrier");var bad=await players.JoinAsync(v,"Broken wallet");
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>
        {
            var hut=new ConstructionSite{Kind=BuildingKind.Hut,PlotId="0,13",Stage=5,PlacedAt=now};
            for(var stage=0;stage<hut.Definition.Stages.Count;stage++)
                foreach(var job in hut.Jobs(stage))if(job.JobId!="hang-door")hut.Record(stage,job.JobId,hut.Required(stage,job.JobId),door.Id);
            city.Sites.Add(hut);
            var other=new ConstructionSite{Kind=BuildingKind.Longhouse,PlotId="0,21",Stage=0,PlacedAt=now};
            other.Record(0,"clear-ground",1,haul.Id);city.Sites.Add(other);
        });
        await players.ArrangeCitizenAsync(door.Id,(c,now)=>{c.CurrentTaskId="hang-door";c.CurrentSiteId="0,13";c.CurrentSiteStage=5;c.CycleSettled=false;c.CycleStartedAt=now.AddMinutes(-10);c.CycleEndsAt=now.AddSeconds(-1);c.ReservedSiteWork=1;});
        await players.ArrangeCitizenAsync(haul.Id,(c,now)=>{c.CurrentTaskId="clear-ground";c.CurrentSiteId="0,21";c.CurrentSiteStage=0;c.CycleSettled=false;c.CycleStartedAt=now.AddMinutes(-10);c.CycleEndsAt=now.AddSeconds(-1);c.ReservedSiteWork=1;});
        await players.ArrangeCitizenAsync(bad.Id,(c,now)=>{c.CurrentTaskId="clear-ground";c.CurrentSiteId="0,21";c.CurrentSiteStage=0;c.CycleSettled=false;c.CycleStartedAt=now.AddMinutes(-10);c.CycleEndsAt=now.AddSeconds(-1);c.ReservedSiteWork=1;});
        await using(var setup=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync())
        {
            var person=await setup.Citizens.SingleAsync(c=>c.Id==bad.Id);
            setup.GameWallets.Add(new GameWallet{VillageId=person.VillageId,AccountId=person.AccountId,Coins=EconomyRules.MaximumBalance});await setup.SaveChangesAsync();
        }
        var service=factory.Services.GetRequiredService<GameService>();
        await using var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var id=await db.Villages.Where(x=>x.Slug==v.Slug).Select(x=>x.Id).SingleAsync();
        await service.SettleVillageAsync(id,[door.Id,bad.Id,haul.Id],default);
        // A second poll/reconnect must not emit or pay for the same finished door again.
        await service.SettleVillageAsync(id,[door.Id,bad.Id,haul.Id],default);
        var city=await db.Villages.AsNoTracking().SingleAsync(x=>x.Id==id);
        Assert.DoesNotContain(city.Sites,x=>x.PlotId=="0,13");
        Assert.Single(city.Structures,x=>x.Kind==BuildingKind.Hut&&x.PlotId=="0,13");
        Assert.Equal(2,city.Sites.Single(x=>x.PlotId=="0,21").Done(0,"clear-ground"));
        Assert.All(await db.Citizens.AsNoTracking().Where(x=>x.Id==door.Id||x.Id==haul.Id).ToArrayAsync(),c=>Assert.True(c.CycleSettled));
        Assert.Equal(2,await db.EconomyReceipts.CountAsync(x=>x.VillageId==id&&x.Fingerprint=="work"));
        Assert.Equal(EconomyRules.MaximumBalance+40,await db.GameWallets.Where(x=>x.VillageId==id).SumAsync(x=>x.Coins));
        Assert.False((await db.Citizens.AsNoTracking().SingleAsync(c=>c.Id==bad.Id)).CycleSettled);
    }

    [Fact]
    public async Task Concurrent_http_starts_claim_one_unit_and_rejections_spend_nothing()
    {
        var village = await Prepare();
        var residents = new[] { await players.JoinAsync(village, "First"), await players.JoinAsync(village, "Second"), await players.JoinAsync(village, "Third") };
        var before = await Task.WhenAll(residents.Select(r => players.LoadCitizenAsync(r.Id)));
        var replies = await Task.WhenAll(residents.Select(r => r.Client.PostAsJsonAsync(village.Path + "/me/job", new { plotId = "9,11", stage = 0, jobId = "stack-pile" })));
        Assert.Single(replies, r => r.IsSuccessStatusCode);
        for (var i = 0; i < replies.Length; i++)
        {
            var saved = await players.LoadCitizenAsync(residents[i].Id);
            if (replies[i].IsSuccessStatusCode) Assert.Equal(1, saved.ReservedSiteWork);
            else
            {
                await AssertRuleBrokenAsync(replies[i], "site.jobBusy");
                Assert.Equal(before[i].StoredEnergy, saved.StoredEnergy);
                Assert.True(saved.CycleSettled);
            }
        }
    }

    [Fact]
    public async Task Independent_service_instances_share_database_admission_lock()
    {
        var village = await Prepare();
        var first = await players.JoinAsync(village, "One");
        var second = await players.JoinAsync(village, "Two");
        var services = new[] { ActivatorUtilities.CreateInstance<GameService>(factory.Services), ActivatorUtilities.CreateInstance<GameService>(factory.Services) };
        var citizens = new[] { await players.LoadCitizenAsync(first.Id), await players.LoadCitizenAsync(second.Id) };
        var outcomes = await Task.WhenAll(services.Select(async (service, i) => {
            try { await service.StartJobAsync(village.Slug, citizens[i].AccountId, new StartJobRequest("9,11", 0, "stack-pile"), default); return "ok"; }
            catch (GameRuleException error) { return error.Code; }
        }));
        Assert.Single(outcomes, result => result == "ok");
        Assert.Single(outcomes, result => result == "site.jobBusy");
    }
}
