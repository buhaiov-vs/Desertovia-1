using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Stats;
using Desertod.Domain.Technologies;
using static Desertod.Domain.Tests.TestWorld;
namespace Desertod.Domain.Tests;

public sealed class ProgressionAndMealsTests
{
    [Theory][InlineData(0,7,3)][InlineData(100,2,8)][InlineData(50,4,6)]
    public void Actual_weapon_contribution_splits_a_finite_stat_reward(double ranged,int strength,int dexterity)
    {
        var c=NewCitizen(NewVillage());var fighter=new RaidFighter{DamageDealt=10000,RangedDamage=ranged*100};
        CombatTraining.Grant(c,fighter);Assert.Equal(strength,c.Stats.StrengthExperience);Assert.Equal(dexterity,c.Stats.DexterityExperience);
    }
    [Fact] public void Idle_defense_has_no_stat_reward_and_talent_cap_is_preserved()
    {
        var c=NewCitizen(NewVillage());CombatTraining.Grant(c,new(){DamageTaken=100,Kills=9});Assert.Equal(0,c.Stats.StrengthExperience+c.Stats.DexterityExperience);
        c.Stats=new(){Strength=10,Dexterity=10,Endurance=10};var fame=c.Renown;CombatTraining.Grant(c,new(){DamageDealt=100,RangedDamage=0});
        Assert.Equal(30,c.Stats.Total);Assert.Equal(fame+10,c.Renown);
    }
    [Theory][InlineData(false,false,10)][InlineData(true,false,0)][InlineData(false,true,0)]
    public void Finishing_a_wave_grants_once_and_manual_or_cancelled_encounters_cannot_farm_stat_XP(bool manual,bool cancel,int expected)
    {
        var v=NewVillage();var c=NewCitizen(v);var raid=new Raid{Enemy=EnemyKind.Zombie,StrikesAt=Now};v.Raids.Add(raid);
        var b=new RaidBattle{Id=raid.Id,VillageId=v.Id,Sector="west",StartsAt=Now,LastTickAt=Now,EndsAt=Now.AddMinutes(4),ManualOverride=manual,
            Units=[new(){Health=0,MaximumHealth=100}],Fighters=[new(){CitizenId=c.Id,Health=50,MaximumHealth=50,DamageDealt=100,RangedDamage=0}]};
        if(cancel)RaidSimulation.Cancel(v,raid,b,[c],Now.AddSeconds(1));else RaidSimulation.Tick(v,raid,b,[c],new HashSet<Guid>(),Now.AddSeconds(1));
        Assert.Equal(expected,c.Stats.StrengthExperience+c.Stats.DexterityExperience);var fame=c.Renown;
        RaidSimulation.Tick(v,raid,b,[c],new HashSet<Guid>(),Now.AddSeconds(2));RaidSimulation.Cancel(v,raid,b,[c],Now.AddSeconds(3));
        Assert.Equal(expected,c.Stats.StrengthExperience+c.Stats.DexterityExperience);Assert.Equal(fame,c.Renown);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void A_real_attack_tracks_ranged_contribution_and_applies_only_an_unexpired_stew_bonus(bool bow)
    {
        (double Hit,double? Ranged) Attack(bool meal){
            var v=NewVillage();var c=NewCitizen(v,(Stat.Strength,3),(Stat.Dexterity,3));c.Weapon=bow?ResourceKind.Bow:ResourceKind.Sword;c.Arrows=6;
            c.MealKind=meal?ResourceKind.Stew:null;c.MealUntil=Now.AddMinutes(20);
            var raid=new Raid{Enemy=EnemyKind.Zombie,StrikesAt=Now};v.Raids.Add(raid);
            var b=new RaidBattle{Id=raid.Id,VillageId=v.Id,Sector="west",Phase="Fighting",StartsAt=Now,LastTickAt=Now,EndsAt=Now.AddMinutes(3),
                Units=[new(){X=14.5,Y=13.5,Health=1000,MaximumHealth=1000,Attack=0,Speed=0}],
                Fighters=[new(){CitizenId=c.Id,X=13.5,Y=13.5,Active=true,Mode="hold",Health=100,MaximumHealth=100,NextAttackAt=Now}]};
            RaidSimulation.Tick(v,raid,b,[c],new HashSet<Guid>{c.Id},Now.AddSeconds(1));return(b.Fighters[0].DamageDealt,b.Fighters[0].RangedDamage);
        }
        var plain=Attack(false);var fed=Attack(true);Assert.True(plain.Hit>0);Assert.Equal(plain.Hit*1.05,fed.Hit,8);Assert.Equal(bow?fed.Hit:0,fed.Ranged);
    }
    private static Village ResearchVillage()
    {
        var v=NewVillage();v.ChosenTechnology=TechnologyCatalog.Writing;v.Knowledge=30;
        v.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="0,0",BuiltAt=Now});
        v.Research.Add(new(){TechnologyId=TechnologyCatalog.Writing,Done=6,Hands=[new(){CitizenId=Guid.NewGuid()}]});return v;
    }
    [Fact] public void Knowledge_spends_once_on_elected_work_but_never_invents_a_crew_and_replay_after_election_is_free()
    {
        var v=ResearchVillage();var p=v.Research.Single();var t=TechnologyCatalog.Get(p.TechnologyId);
        Assert.True(TechnologyRules.InvestKnowledge(v,t.Id,10,Now));Assert.Equal(20,v.Knowledge);Assert.Equal(8,p.Done);Assert.Equal(2,p.KnowledgeWork);Assert.Null(p.ResearchedAt);Assert.Single(p.Hands);
        var c=NewCitizen(v);TechnologyRules.Research(v,c,t,1,10,Now);Assert.Null(p.ResearchedAt);
        TechnologyRules.Research(v,NewCitizen(v),t,1,10,Now);Assert.NotNull(p.ResearchedAt);
        v.ChosenTechnology=TechnologyCatalog.StoneTools;Assert.False(TechnologyRules.InvestKnowledge(v,t.Id,10,Now));Assert.Equal(20,v.Knowledge);
    }
    [Fact] public void Knowledge_requires_existing_work_place_choice_prerequisites_and_budget_before_charging()
    {
        var v=ResearchVillage();Assert.Equal("technology.unknown",Assert.Throws<GameRuleException>(()=>TechnologyRules.InvestKnowledge(v,null!,3,Now)).Code);v.Knowledge=9;Assert.Equal("research.knowledgeInsufficient",Assert.Throws<GameRuleException>(()=>TechnologyRules.InvestKnowledge(v,TechnologyCatalog.Writing,3,Now)).Code);Assert.Equal(6,v.Research.Single().Done);Assert.Equal(9,v.Knowledge);
        v.Knowledge=100;v.Structures.Single().Damage=100;Assert.Equal(0,TechnologyRules.KnowledgeWorkAvailable(v,TechnologyCatalog.Get(TechnologyCatalog.Writing)));
        v.Structures.Single().Damage=0;v.Research.Single().Hands.Clear();Assert.Equal(0,TechnologyRules.KnowledgeWorkAvailable(v,TechnologyCatalog.Get(TechnologyCatalog.Writing)));
        v.Research.Single().AddHand(Guid.NewGuid());v.ChosenTechnology=TechnologyCatalog.Mining;Assert.Equal(0,TechnologyRules.KnowledgeWorkAvailable(v,TechnologyCatalog.Get(TechnologyCatalog.Mining)));Assert.Equal(100,v.Knowledge);
    }
    [Theory][InlineData(ResourceKind.Porridge)][InlineData(ResourceKind.Stew)]
    public void A_meal_is_one_personal_portion_not_instant_energy_or_health_and_cannot_stack_or_extend(ResourceKind kind)
    {
        var v=NewVillage();var c=NewCitizen(v);PersonalInventory.Add(c,kind,2,0);c.StoredEnergy=2;var energy=c.StoredEnergy;
        PreparedMeals.Eat(c,v,kind,Now);Assert.Equal(energy,c.StoredEnergy);Assert.Equal(1,PersonalInventory.Amount(c,kind));Assert.Equal(Now.AddMinutes(20),c.MealUntil);
        Assert.Equal("meal.cooldown",Assert.Throws<GameRuleException>(()=>PreparedMeals.Eat(c,v,kind,Now.AddMinutes(1))).Code);
        Assert.Equal("meal.cooldown",Assert.Throws<GameRuleException>(()=>PreparedMeals.Eat(c,v,kind,Now.AddMinutes(21))).Code);
        PreparedMeals.Eat(c,v,kind,Now.AddMinutes(30));Assert.Equal(0,PersonalInventory.Amount(c,kind));
    }
    [Fact] public void Public_meals_cannot_be_eaten_remotely_and_wounded_citizens_do_not_consume_their_portion()
    {
        var v=NewVillage();var c=NewCitizen(v);v.Store(ResourceKind.Stew,4);
        Assert.Equal("meal.none",Assert.Throws<GameRuleException>(()=>PreparedMeals.Eat(c,v,ResourceKind.Stew,Now)).Code);Assert.Equal(4,v.Stock(ResourceKind.Stew));
        PersonalInventory.Add(c,ResourceKind.Stew,1,0);c.InjuredUntil=Now.AddHours(1);
        Assert.Throws<GameRuleException>(()=>PreparedMeals.Eat(c,v,ResourceKind.Stew,Now));Assert.Equal(1,PersonalInventory.Amount(c,ResourceKind.Stew));Assert.Null(c.LastMealAt);
    }
    [Fact] public void Porridge_affects_the_actual_work_price_only_until_expiry_and_stew_never_restores_stamina()
    {
        var v=NewVillage();var c=NewCitizen(v);PersonalInventory.Add(c,ResourceKind.Porridge,1,0);PreparedMeals.Eat(c,v,ResourceKind.Porridge,Now);
        var context=new WorkContext{Village=v};var task=Task("forage");var price=WorkEnergy.WithMedical(c,WorkEnergy.TaskCost(v,task,Now),Now);var before=c.CurrentEnergy(Now,v);
        CycleEngine.Start(c,task,context,Now);Assert.Equal(before-price,c.StoredEnergy,8);Assert.Equal(1.125,price,8);
        Assert.Equal(1,PreparedMeals.WorkMultiplier(c,Now.AddMinutes(20)));Assert.Equal(1,PreparedMeals.AttackMultiplier(c,Now));
        c.MealKind=ResourceKind.Stew;c.MealUntil=Now.AddMinutes(20);Assert.Equal(1.05,PreparedMeals.AttackMultiplier(c,Now));Assert.Equal(1,PreparedMeals.AttackMultiplier(c,Now.AddMinutes(20)));
    }
    [Theory][InlineData("cook-porridge",BuildingKind.Bakery,TechnologyCatalog.Baking,ResourceKind.Porridge,ResourceKind.Grain,2)]
    [InlineData("cook-stew",BuildingKind.Tavern,TechnologyCatalog.Brewing,ResourceKind.Stew,ResourceKind.Food,3)]
    public void Recipes_are_real_paid_work_with_existing_materials_and_a_storage_output(string id,BuildingKind building,string technology,ResourceKind output,ResourceKind input,int amount)
    {
        var v=NewVillage();var c=NewCitizen(v);v.Structures.Add(new(){Kind=building,PlotId="0,0",BuiltAt=Now});v.Research.Add(new(){TechnologyId=technology,ResearchedAt=Now});v.Store(ResourceKind.Wood,5);if(input==ResourceKind.Grain)v.Store(input,10);
        var context=new WorkContext{Village=v};var before=v.Stock(input);var wood=v.Stock(ResourceKind.Wood);
        CycleEngine.Start(c,Task(id),context,Now);var report=Finish(c,context);
        Assert.Equal(before-amount,v.Stock(input));Assert.Equal(wood-1,v.Stock(ResourceKind.Wood));Assert.Equal(1,c.Carried(output)+v.Stock(output));Assert.Contains(report.Produced,g=>g.Resource==output&&g.Amount==1);
    }
}
