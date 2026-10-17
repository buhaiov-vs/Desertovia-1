using System.Net;
using System.Net.Http.Json;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Technologies;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;
namespace Desertod.Api.Tests;
[Collection(ApiCollection.Name)]
public sealed class ProgressionAndMealsApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    [Fact] public async Task Research_requires_ruler_and_spends_only_once_even_when_the_investment_finishes_the_discovery()
    {
        var v=await players.FoundVillageAsync("Knowledge vote");var c=await players.JoinAsync(v);
        await using(var db=await Db()){
            var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);city.Knowledge=30;city.ChosenTechnology=TechnologyCatalog.Writing;
            city.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="0,0",BuiltAt=factory.Clock.GetUtcNow()});
            city.Research.Add(new(){TechnologyId=TechnologyCatalog.Writing,Done=6,Hands=[new(){CitizenId=c.Id}]});await db.SaveChangesAsync();
        }
        var path=v.Path+"/research/knowledge";var body=new{technologyId=TechnologyCatalog.Writing};
        Assert.Equal(HttpStatusCode.Forbidden,(await c.Client.PostAsJsonAsync(path,body)).StatusCode);
        await OkJsonAsync(await v.Tsar.PutAsJsonAsync(v.Path+$"/citizens/{c.Id}/governor",new{isGovernor=true}));
        var response=await OkJsonAsync(await c.Client.PostAsJsonAsync(path,body));
        var tech=response.GetProperty("technologies").EnumerateArray().Single(t=>t.GetProperty("id").GetString()==TechnologyCatalog.Writing);
        Assert.True(tech.GetProperty("discovered").GetBoolean());Assert.Equal(2,tech.GetProperty("knowledgeWork").GetInt32());
        await OkJsonAsync(await c.Client.PostAsJsonAsync(path,body));await OkJsonAsync(await v.Tsar.PostAsJsonAsync(path,body));
        await using var verify=await Db();var restored=await verify.Villages.SingleAsync(x=>x.Slug==v.Slug);
        Assert.Equal(20,restored.Knowledge);Assert.Single(restored.Research.Single().Hands);Assert.Equal(8,restored.Research.Single().Done);
    }
    [Fact] public async Task Concurrent_meal_clicks_consume_one_portion_and_survive_reload_with_no_energy_or_stat_reward()
    {
        var v=await players.FoundVillageAsync("Meals");var c=await players.JoinAsync(v);
        await using(var db=await Db()){
            var citizen=await db.Citizens.SingleAsync(x=>x.Id==c.Id);citizen.StoredEnergy=2;citizen.EnergyUpdatedAt=factory.Clock.GetUtcNow();
            PersonalInventory.Add(citizen,ResourceKind.Porridge,2,7);await db.SaveChangesAsync();
        }
        var responses=await Task.WhenAll(c.Client.PostAsJsonAsync(v.Path+"/me/meal",new{resource="Porridge"}),c.Client.PostAsJsonAsync(v.Path+"/me/meal",new{resource="Porridge"}));
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);await AssertRuleBrokenAsync(Assert.Single(responses,r=>r.StatusCode!=HttpStatusCode.OK),"meal.cooldown");
        var dto=await OkJsonAsync(await c.Client.GetAsync(v.Path+"/me"));Assert.Equal("Porridge",dto.GetProperty("mealKind").GetString());

        await using var verify=await Db();var restored=await verify.Citizens.SingleAsync(x=>x.Id==c.Id);Assert.Equal(1,PersonalInventory.Amount(restored,ResourceKind.Porridge));Assert.Equal(0,restored.Stats.StrengthExperience);Assert.NotNull(restored.LastMealAt);Assert.Equal(restored.LastMealAt.Value.AddMinutes(20),restored.MealUntil);Assert.Equal(restored.MealUntil,dto.GetProperty("mealUntil").GetDateTimeOffset());Assert.Equal(2,restored.StoredEnergy,6);
    }
    [Fact] public async Task Missing_meal_and_unselected_research_leave_goods_and_knowledge_intact()
    {
        var v=await players.FoundVillageAsync("No remote meals");var c=await players.JoinAsync(v);
        await AssertRuleBrokenAsync(await c.Client.PostAsJsonAsync(v.Path+"/me/meal",new{resource="Stew"}),"meal.none");
        await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/research/knowledge",new{technologyId=TechnologyCatalog.Writing}),"research.knowledgeUnavailable");
        await using var db=await Db();Assert.Null((await db.Citizens.SingleAsync(x=>x.Id==c.Id)).LastMealAt);Assert.Equal(0,(await db.Villages.SingleAsync(x=>x.Slug==v.Slug)).Knowledge);
    }
}
