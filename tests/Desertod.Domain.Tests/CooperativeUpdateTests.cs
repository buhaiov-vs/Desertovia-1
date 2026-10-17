using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Storage;
using Desertod.Domain.World;
namespace Desertod.Domain.Tests;

public class CooperativeUpdateTests
{
    private static readonly DateTimeOffset Now=TestWorld.Now;
    [Fact] public void EveryCommunalStageReferencesARealJob()
    {
        foreach(var stage in BuildingCatalog.Get(BuildingKind.Longhouse).Stages)
            foreach(var job in stage.Jobs) Assert.NotNull(BuildingCatalog.FindJob(job.JobId));
    }
    [Fact] public void FixedBinsDoNotCompeteAndLegacyOverflowSurvives()
    {
        var store=StorageBuilding.Create(StorageKind.WoodPile,"1,1",Now);
        Assert.Equal(30,store.Put(ResourceKind.Wood,100));
        Assert.Equal(30,store.Put(ResourceKind.Reeds,100));
        store.Goods.Single(g=>g.Kind==ResourceKind.Wood).Amount=70;
        Assert.Equal(0,store.Put(ResourceKind.Wood,1));Assert.Equal(70,store.Take(ResourceKind.Wood,100));
        Assert.Equal(30,store.Amount(ResourceKind.Reeds));
    }
    [Fact] public void CommonHeapHasPerResourceAndTotalLimits()
    {
        var store=StorageBuilding.Create(StorageKind.Heap,"1,1",Now);
        foreach(var kind in new[]{ResourceKind.Wood,ResourceKind.Stone,ResourceKind.Food,ResourceKind.Reeds})Assert.Equal(10,store.Put(kind,20));
        Assert.Equal(0,store.Put(ResourceKind.Grain,1));
    }
    [Fact] public void NewBridgeIsStagedButExistingProjectsKeepTheirRequirements()
    {
        var old=new VillageProject{DefinitionId=ProjectCatalog.TsarBridge};
        var fresh=VillageProject.Start(Guid.NewGuid(),ProjectCatalog.Find(ProjectCatalog.TsarBridge)!,Now);
        Assert.Equal(60,old.Definition.BuildCycles);Assert.Equal(218,fresh.Definition.BuildCycles);
        Assert.Equal(12,fresh.Definition.SurveyCycles);Assert.Equal(0,fresh.BuildLimit);
        fresh.Materials.ForEach(m=>m.Delivered=fresh.Required(m.Kind.ToString()));Assert.Equal(40,fresh.BuildLimit);
        fresh.BuildProgress=40;Assert.Equal(90,fresh.BuildLimit);
    }
    [Fact] public void FarBankGuardAppliesToWalkingAndDirectResourceClicks()
    {
        var village=TestWorld.NewVillage();var citizen=TestWorld.NewCitizen(village);
        var point=new TilePoint(40,17);
        Assert.Equal("bridge.closed",Assert.Throws<GameRuleException>(()=>citizen.GoTo(point,village,Now)).Code);
        Assert.Equal("bridge.closed",Assert.Throws<GameRuleException>(()=>WorkSpots.Check(village,GameCatalog.FindTask("gather-stone")??GameCatalog.Tasks.First(t=>t.Workplace=="stones"),point,Now)).Code);
        village.BridgeOpened=true;citizen.GoTo(point,village,Now);Assert.NotNull(citizen.Trip);
    }
    [Fact] public void ExistingHomesStayStableAndCommunalHouseExpands()
    {
        var village=TestWorld.NewVillage();var home=new Structure{Kind=BuildingKind.Longhouse,PlotId="1,1",BuiltAt=Now};village.Structures.Add(home);
        var citizens=Enumerable.Range(0,13).Select(_=>TestWorld.NewCitizen(village)).ToArray();
        ShelterRules.Assign(village,citizens,Now);Assert.Equal(12,citizens.Count(c=>c.HomePlotId==home.PlotId));
        home.Level=2;ShelterRules.Assign(village,citizens,Now.AddMinutes(1));Assert.All(citizens,c=>Assert.Equal(home.PlotId,c.HomePlotId));Assert.Equal(24,ShelterRules.Beds(home));
    }
    [Fact] public void ExhaustedWorkIsSlowButWalkingRetainsItsSpeedAndFaintsOnce()
    {
        var village=TestWorld.NewVillage();var c=TestWorld.NewCitizen(village);var task=GameCatalog.Tasks.First(t=>t.Workplace=="stones");
        c.StoredEnergy=0;c.EnergyUpdatedAt=Now;
        var context=new WorkContext{Village=village};var now=Now;
        for(var i=0;i<3;i++)
        {
            CycleEngine.Start(c,task,context,now);
            Assert.True(c.CycleTired);Assert.True(c.CycleEndsAt-now>=TimeSpan.FromSeconds(task.WorkSeconds*3));
            now=c.CycleEndsAt!.Value;CycleEngine.Settle(c,context,now);
        }
        Assert.NotNull(c.ExhaustedUntil);Assert.Equal("citizen.exhausted",Assert.Throws<GameRuleException>(()=>CycleEngine.Start(c,task,context,now)).Code);
        Assert.Null(CycleEngine.Settle(c,context,now));
    }
    [Fact] public void SkillDoesNotSpeedUpGatheringOrBuilding()
    {
        var village=TestWorld.NewVillage();var citizen=TestWorld.NewCitizen(village).WithSkill(Skill.Building,9).WithSkill(Skill.Woodcutting,9);
        Assert.Equal(GameRules.CraftLength,SkillRules.WorkTime(citizen,Skill.Building,0,GameRules.CraftLength));
        Assert.Equal(GameRules.CycleLength,SkillRules.WorkTime(citizen,Skill.Woodcutting,0,GameRules.CycleLength));
    }
    [Fact] public void DemolitionRefusesOccupiedOrStockedBuildings()
    {
        var village=TestWorld.NewVillage();var store=StorageBuilding.Create(StorageKind.WoodPile,"1,1",Now);village.Storages.Add(store);
        store.Goods.Add(new StoredGood{Kind=ResourceKind.Wood,Amount=4});
        var order=BuildingOperations.Request(village,Guid.NewGuid(),store.PlotId,"demolish",Now);
        Assert.Equal("building.emptyStorage",Assert.Throws<GameRuleException>(()=>BuildingOperations.Approve(village,order,[],Now)).Code);
        Assert.Empty(village.Sites);Assert.Equal(4,store.Amount(ResourceKind.Wood));
    }
    [Fact] public void MedicalEffectsExpireWithoutDestroyingLoadOrStats()
    {
        var village=TestWorld.NewVillage();var c=TestWorld.NewCitizen(village);c.Carry(ResourceKind.Wood,10);
        c.Recovery=new ExternalRecovery{Available=true,EffectKind="cast",EffectUntil=Now.AddHours(4)};
        Assert.Equal(7,StorageRules.CarryLimit(c,Now));Assert.Equal(.8,c.MedicalWalk(Now));Assert.Equal(10,c.Carried(ResourceKind.Wood));
        Assert.Equal(10,StorageRules.CarryLimit(c,Now.AddHours(5)));Assert.Equal(1,c.MedicalWalk(Now.AddHours(5)));
    }
    [Fact] public void StageRewardPreservesPoolAndUsefulContributionWeights()
    {
        var first=Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second=Guid.Parse("00000000-0000-0000-0000-000000000002");
        var third=Guid.Parse("00000000-0000-0000-0000-000000000003");
        var shares=ProjectRules.PoolShares(new Dictionary<Guid,double>{{first,8},{second,1},{third,1}},5);
        Assert.Equal(5,shares.Values.Sum());Assert.Equal(4,shares[first]);Assert.Equal(1,shares[second]);Assert.Equal(0,shares[third]);
        Assert.All(ProjectRules.PoolShares(new Dictionary<Guid,double>{{first,0},{second,0}},5).Values,amount=>Assert.Equal(0,amount));
    }
    [Fact] public void RepeatedWorkDoesNotPretendToBeMultipleCrewMembers()
    {
        var project=VillageProject.Start(Guid.NewGuid(),ProjectCatalog.Find(ProjectCatalog.TsarBridge)!,Now);
        var first=Guid.NewGuid();var second=Guid.NewGuid();var third=Guid.NewGuid();
        project.RegisterHand(first,20);Assert.False(project.HasCrew(first,3));
        project.RegisterHand(Guid.NewGuid(),2,first);Assert.False(project.HasCrew(first,3));
        project.RegisterHand(second,2);Assert.False(project.HasCrew(first,3));Assert.True(project.HasCrew(third,3));
        project.BuildProgress=40;Assert.False(project.HasCrew(third,3));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SupervisionRewardsActualProgressRatherThanWhoIsStillWorkingAtSettlement(bool useful)
    {
        var village=TestWorld.NewVillage();
        var project=VillageProject.Start(village.Id,ProjectCatalog.Find(ProjectCatalog.TsarBridge)!,Now);
        project.SurveyProgress=project.Definition.SurveyCycles;
        var engineer=TestWorld.NewCitizen(village,(Desertod.Domain.Stats.Stat.Intelligence,3)).WithSkill(Skill.Building,1);
        var context=new WorkContext{Village=village,Project=project,BuildersWorking=1};
        CycleEngine.Start(engineer,GameCatalog.FindTask("supervise")!,context,Now);
        if(useful)project.Record(Guid.NewGuid(),VillageProject.BuildKey,2);
        var report=CycleEngine.Settle(engineer,new WorkContext{Village=village,Project=project,BuildersWorking=0},engineer.CycleEndsAt!.Value)!;
        Assert.Equal(!useful,report.Overflowed);
        if(useful){Assert.True(report.SkillExperience>0);Assert.Equal(1,report.ProjectContribution);}
        else {Assert.Equal(0,report.SkillExperience);Assert.Equal(0,report.ProjectContribution);Assert.Equal(0,report.RenownGained);}
    }
}
