using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using static Desertod.Domain.Tests.TestWorld;
namespace Desertod.Domain.Tests;

public class FeedbackFixesTests
{
    [Fact]
    public void Ruined_untouched_blueprint_can_be_removed_without_healing_an_existing_structure()
    {
        var v=NewVillage();
        var existing=new Structure{Kind=BuildingKind.Hut,PlotId="1,1",Damage=100,RuinedAt=Now};
        v.Structures.Add(existing);
        v.Sites.Add(new(){Kind=BuildingKind.Hut,PlotId="1,1",PlacedAt=Now,Damage=100,RuinedAt=Now});
        ConstructionRules.Cancel(v,"1,1");
        Assert.Empty(v.Sites);Assert.Equal(100,existing.Damage);Assert.Equal(Now,existing.RuinedAt);
    }
    [Fact]
    public void Invested_ruins_still_require_dismantling_and_cannot_be_erased()
    {
        var v=NewVillage();
        v.Sites.Add(new(){Kind=BuildingKind.Hut,PlotId="1,1",PlacedAt=Now,Damage=100,RuinedAt=Now,
            Progress=[new(){JobId="bring-logs",Stage=0,Done=8}]});
        Assert.Equal("building.cannotCancel",Assert.Throws<GameRuleException>(()=>ConstructionRules.Cancel(v,"1,1")).Code);
        Assert.Single(v.Sites);Assert.Equal(8,v.Sites[0].Progress[0].Done);
    }
    [Fact]
    public void Work_completion_retains_sharpened_gear_during_live_raid_then_returns_it_once()
    {
        var v=NewVillage();var c=NewCitizen(v);c.Weapon=ResourceKind.Sword;c.WeaponSharpen=3;
        c.Armor=ResourceKind.Shield;
        var raid=new Raid{Enemy=EnemyKind.Zombie,Count=1,AnnouncedAt=Now,StrikesAt=Now};v.Raids.Add(raid);
        CombatRules.ReturnWorkEquipment(c,v,Now);
        Assert.Equal(ResourceKind.Sword,c.Weapon);Assert.Equal(3,c.WeaponSharpen);Assert.Equal(0,v.Stock(ResourceKind.Sword));
        raid.Outcome=RaidOutcome.Repelled;
        CombatRules.ReturnWorkEquipment(c,v,Now.AddMinutes(5));
        Assert.Null(c.Weapon);Assert.Equal(1,v.Stock(ResourceKind.Sword));
        CombatRules.ReturnWorkEquipment(c,v,Now.AddMinutes(5));Assert.Equal(1,v.Stock(ResourceKind.Sword));
        Assert.Equal(3,Assert.Single(v.Storages.SelectMany(s=>s.Goods),g=>g.Kind==ResourceKind.Sword).Sharpen);
    }
    [Fact]
    public void Future_warning_does_not_keep_equipment_on_an_ordinary_worker()
    {
        var v=NewVillage();var c=NewCitizen(v);c.Weapon=ResourceKind.Spear;
        v.Raids.Add(new(){Enemy=EnemyKind.Zombie,Count=1,AnnouncedAt=Now,StrikesAt=Now.AddMinutes(5)});
        CombatRules.ReturnWorkEquipment(c,v,Now);Assert.Null(c.Weapon);Assert.Equal(1,v.Stock(ResourceKind.Spear));
    }
}
