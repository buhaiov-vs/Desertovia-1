using System.Net;
using System.Net.Http.Json;
using Desertod.Api.Game;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Storage;
using Desertod.Domain.Technologies;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;
namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class OwnerCorrectionApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    [Fact]
    public async Task New_world_contains_one_central_castle_and_still_accepts_new_players()
    {
        var v=await players.FoundVillageAsync("New royal centre");await players.JoinAsync(v);
        var state=(await OkJsonAsync(await v.Tsar.GetAsync(v.Path))).GetProperty("state");
        var castle=Assert.Single(state.GetProperty("structures").EnumerateArray(),s=>s.GetProperty("kind").GetString()=="Castle");
        Assert.Equal("14,9",castle.GetProperty("plotId").GetString());Assert.Equal(1,castle.GetProperty("level").GetInt32());
    }
    [Fact]
    public async Task Concurrent_duplicate_heal_spends_one_remedy_and_persists_health_and_receipt()
    {
        var v=await players.FoundVillageAsync("Healing king");Guid battleId;
        await using(var db=await Db())
        {
            var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);var now=factory.Clock.GetUtcNow();
            var store=StorageBuilding.Create(StorageKind.Heap,"1,1",now);store.Put(ResourceKind.Remedy,2);city.Storages.Add(store);
            var b=new RaidBattle{VillageId=city.Id,Sector="north",Phase="Fighting",StartsAt=now,LastTickAt=now,EndsAt=now.AddMinutes(4),Fighters=[new(){IsTsar=true,AccountId=city.OwnerAccountId,Active=true,Guard=true,Health=10,MaximumHealth=80}]};
            db.RaidBattles.Add(b);battleId=b.Id;await db.SaveChangesAsync();
        }
        var id=Guid.NewGuid();var uri=v.Path+$"/battles/{battleId}/order";
        var responses=await Task.WhenAll(v.Tsar.PutAsJsonAsync(uri,new{mode="heal",requestId=id}),v.Tsar.PutAsJsonAsync(uri,new{mode="heal",requestId=id}));
        Assert.All(responses,x=>Assert.Equal(HttpStatusCode.OK,x.StatusCode));
        await using var verify=await Db();var battle=await verify.RaidBattles.SingleAsync(x=>x.Id==battleId);var cityAfter=await verify.Villages.SingleAsync(x=>x.Slug==v.Slug);
        Assert.Equal(26,Assert.Single(battle.Fighters).Health);Assert.Equal(1,cityAfter.Stock(ResourceKind.Remedy));
        Assert.NotNull(battle.Fighters[0].HealReadyAt);Assert.Contains(id.ToString("N"),battle.Fighters[0].HealingReceipts);
    }
    [Fact]
    public async Task Monastery_contract_checks_bridge_arrival_and_knowledge_then_is_durable_and_idempotent()
    {
        var v=await players.FoundVillageAsync("Monastery contract");var key=Guid.NewGuid();var uri=v.Path+"/monastery";
        await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(uri,new{requestId=key}),"bridge.closed");
        await using(var db=await Db())
        {
            var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);city.BridgeOpened=true;await db.SaveChangesAsync();
        }
        await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(uri,new{requestId=key}),"monastery.distance");
        await using(var db=await Db())
        {
            var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);city.TsarSpot=GameService.MonasteryYard;await db.SaveChangesAsync();
        }
        await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(uri,new{requestId=key}),"monastery.knowledge");
        await using(var db=await Db())
        {
            var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);city.Research.Add(new(){TechnologyId=TechnologyCatalog.HerbLore,ResearchedAt=factory.Clock.GetUtcNow()});
            var s=StorageBuilding.Create(StorageKind.TownHall,"1,1",factory.Clock.GetUtcNow());s.Put(ResourceKind.Wood,10);s.Put(ResourceKind.Food,6);city.Storages.Add(s);await db.SaveChangesAsync();
        }
        for(var i=0;i<2;i++)await OkJsonAsync(await v.Tsar.PostAsJsonAsync(uri,new{requestId=key}));
        await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(uri,new{requestId=Guid.NewGuid()}),"monastery.cooldown");
        await using var verify=await Db();var cityAfter=await verify.Villages.SingleAsync(x=>x.Slug==v.Slug);
        Assert.Equal(1,cityAfter.Stock(ResourceKind.Remedy));Assert.Equal(0,cityAfter.Stock(ResourceKind.Wood));Assert.Equal(0,cityAfter.Stock(ResourceKind.Food));
        Assert.Single(await verify.EconomyReceipts.Where(x=>x.VillageId==cityAfter.Id&&x.RequestId==key).ToListAsync());
    }

    [Fact]
    public async Task Castle_defeat_cancels_future_waves_once_and_preserves_people_and_their_progress()
    {
        var v=await players.FoundVillageAsync("Castle defeat");var resident=await players.JoinAsync(v);Guid villageId,firstId,nextId;
        await using(var db=await Db())
        {
            var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);villageId=city.Id;var now=factory.Clock.GetUtcNow();
            var castle=city.Structures.Single(s=>s.Kind==BuildingKind.Castle);castle.PlotId="14,18";castle.Damage=99;
            var c=await db.Citizens.SingleAsync(x=>x.Id==resident.Id);c.Renown=73;
            var yard=Desertod.Domain.World.WorldMap.Yard(castle.PlotId);
            var first=new Raid{Source=RaidSource.Undead,EngineVersion=2,Enemy=EnemyKind.Zombie,Count=1,Wave=1,Waves=2,AnnouncedAt=now,StrikesAt=now.AddSeconds(-1)};
            var next=new Raid{Source=RaidSource.Undead,EngineVersion=2,Enemy=EnemyKind.Zombie,Count=1,Wave=2,Waves=2,AnnouncedAt=now,StrikesAt=now.AddMinutes(1)};
            city.Raids.AddRange([first,next]);firstId=first.Id;nextId=next.Id;
            foreach(var raid in new[]{first,next})db.RaidBattles.Add(new(){Id=raid.Id,VillageId=city.Id,Sector="north",Phase=raid==first?"Fighting":"Warning",
                StartsAt=raid.StrikesAt,LastTickAt=raid.StrikesAt,EndsAt=raid.StrikesAt.AddMinutes(4),
                Units=[new(){X=yard.X,Y=yard.Y,Health=100,MaximumHealth=100,Speed=0}]});
            RaidRules.UpdateNextRaidAt(city);await db.SaveChangesAsync();
        }
        var service=factory.Services.GetRequiredService<GameService>();await service.RaidVillageAsync(villageId,default);await service.RaidVillageAsync(villageId,default);
        await using var verify=await Db();var cityAfter=await verify.Villages.SingleAsync(x=>x.Id==villageId);
        Assert.NotNull(cityAfter.DefeatedAt);Assert.Equal(firstId,cityAfter.DefeatedByBattleId);Assert.Null(cityAfter.NextRaidAt);
        Assert.Equal(RaidOutcome.Lost,cityAfter.Raids.Single(x=>x.Id==firstId).Outcome);
        Assert.Equal(RaidOutcome.Cancelled,cityAfter.Raids.Single(x=>x.Id==nextId).Outcome);
        var cancelled=await verify.RaidBattles.SingleAsync(x=>x.Id==nextId);Assert.Equal("Cancelled",cancelled.Phase);Assert.Equal(0,cityAfter.Raids.Single(x=>x.Id==nextId).Killed);
        Assert.Equal(73,(await players.LoadCitizenAsync(resident.Id)).Renown);
        await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/undead",new{enemy="Skeleton",size="Small"}),"village.defeated");
    }
}
