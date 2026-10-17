using System.Text.Json;

namespace Desertod.Domain.Combat;

/// <summary>Снимок настроек события. После объявления не пересчитывается.</summary>
public sealed class RaidSettings
{
    public string Preset { get; set; } = "mixed";
    public string Mode { get; set; } = "automatic";
    public EnemyKind Enemy { get; set; } = EnemyKind.Zombie;
    public string? Sector { get; set; }
    public int Count { get; set; } = 24;
    public int Waves { get; set; } = 1;
    public int WarningSeconds { get; set; } = 180;
    public int WaveIntervalSeconds { get; set; } = 120;
    public int DurationSeconds { get; set; } = 240;
    public double BudgetMultiplier { get; set; } = 1;
    public double HealthMultiplier { get; set; } = 1;
    public double AttackMultiplier { get; set; } = 1;
    public double PropertyMultiplier { get; set; } = 1;
    public double SpeedMultiplier { get; set; } = 1;
    public int WeakPercent { get; set; } = 35;
    public int ElitePercent { get; set; } = 20;
    public string Tactic { get; set; } = "mixed";
    public double RetreatSurvivorFraction { get; set; } = .3;
    public int PropertyLimit { get; set; } = 360;
    public int WallLimit { get; set; } = 600;
    public int LootLimit { get; set; } = 24;
    public double TheftFraction { get; set; } = .02;
    public bool BossStrike { get; set; } = true;
    public int BossWarningSeconds { get; set; } = 3;
    public int BossSecondWarningSeconds { get; set; } = 2;
    public int BossCooldownSeconds { get; set; } = 9;
    public int BossSecondCooldownSeconds { get; set; } = 6;
    public bool BossSecondPhase { get; set; } = true;
    public List<RaidSpeciesShare> Species { get; set; } = [];

    public static RaidSettings ForPreset(string preset) => preset switch
    {
        "boss" => new() { Preset="boss",Count=2,DurationSeconds=360,RetreatSurvivorFraction=0 },
        "swarm" => new() { Preset="swarm",Count=48,WeakPercent=100,ElitePercent=0,Tactic="breach" },
        "mixed" => new(),
        _ => throw new GameRuleException("raid.settings","Choose boss, mixed or swarm preset.")
    };

    public RaidSettings Copy() => JsonSerializer.Deserialize<RaidSettings>(JsonSerializer.Serialize(this))!;

    public void Validate(bool ownerManualAllowed)
    {
        void Require(bool valid) { if(!valid)throw new GameRuleException("raid.settings","Invalid raid settings. Check the allowed ranges and composition."); }
        void Range(double value,double min,double max) => Require(double.IsFinite(value)&&value>=min&&value<=max);
        Require(Preset is "boss" or "mixed" or "swarm" or "elite");
        Require(Mode is "automatic" or "manual");
        Require(Mode!="manual"||ownerManualAllowed);
        Require(Enum.IsDefined(Enemy)&&!EnemyCatalog.Get(Enemy).DungeonOnly);
        Require(Sector is null or "north" or "west" or "south");
        Range(Count,1,Preset=="boss"?3:RaidBalance.MaximumUnits);
        Range(Waves,1,5);Range(WarningSeconds,30,900);Range(WaveIntervalSeconds,15,600);Range(DurationSeconds,30,360);
        Range(BudgetMultiplier,.5,ownerManualAllowed?3:1.5);
        Range(HealthMultiplier,.25,20);Range(AttackMultiplier,.25,10);Range(PropertyMultiplier,.25,5);Range(SpeedMultiplier,.25,2);
        Range(WeakPercent,0,100);Range(ElitePercent,0,100);Require(WeakPercent+ElitePercent<=100);
        Require(Tactic is "mixed" or "entry" or "breach" or "plunder" or "hunt");
        Range(RetreatSurvivorFraction,0,.8);Range(PropertyLimit,0,1000);Range(WallLimit,0,1500);
        Range(LootLimit,0,500);Range(TheftFraction,0,.1);
        Range(BossWarningSeconds,1,10);Range(BossSecondWarningSeconds,1,10);
        Range(BossCooldownSeconds,3,30);Range(BossSecondCooldownSeconds,3,30);
        if(Species is null)throw new GameRuleException("raid.settings","Choose a valid composition.");
        Require(Species.Count<=5&&Species.All(s=>s is not null));
        if(!ownerManualAllowed)
        {
            Range(HealthMultiplier,.5,2);Range(AttackMultiplier,.5,2);Range(PropertyMultiplier,.5,2);Range(SpeedMultiplier,.5,1.5);
            Range(PropertyLimit,0,360);Range(WallLimit,0,600);Range(LootLimit,0,48);Range(TheftFraction,0,.03);
        }
        if(Species.Count>0)
        {
            Require(Species.Select(s=>s.Enemy).Distinct().Count()==Species.Count&&Species.Sum(s=>s.Percent)==100);
            foreach(var species in Species)
            {Require(Enum.IsDefined(species.Enemy)&&!EnemyCatalog.Get(species.Enemy).DungeonOnly);Range(species.Percent,1,100);}
        }
    }

    public EnemyKind KindAt(int index) => Species.Count==0?Enemy:SpeciesAt(index*61%100);
    private EnemyKind SpeciesAt(int slot)
    {
        var total=0;
        foreach(var group in Species){total+=group.Percent;if(slot<total)return group.Enemy;}
        return Species[^1].Enemy;
    }
    public string VariantAt(int index) => Preset switch
    {
        "boss"=>"boss","swarm"=>"weak","elite"=>"strong",
        _=>(index*37%100)<WeakPercent?"weak":(index*37%100)<WeakPercent+ElitePercent?"strong":"regular"
    };
}

public sealed class RaidSpeciesShare
{
    public EnemyKind Enemy { get; set; }
    public int Percent { get; set; }
}

public sealed class SavedRaidPreset
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public RaidSettings Settings { get; set; } = new();
}

public sealed record RaidUnitPlan(EnemyKind Enemy,string Variant,double Health,double Attack,double Speed,double Cost);
public sealed record RaidPreview(double RecommendedBudget,double ActualCost,int Count,double TotalHealth,
    double TotalAttack,string Risk,IReadOnlyList<RaidUnitPlan> Units);

public static class RaidSettingsPlanner
{
    public static RaidPreview Preview(RaidSettings settings,double readinessBudget,bool ownerManualAllowed,bool legacyFallback=false)
    {
        settings.Validate(ownerManualAllowed);
        var units=new List<RaidUnitPlan>();
        var budget=readinessBudget*settings.BudgetMultiplier;
        var spent=0d;
        for(var i=0;i<settings.Count;i++)
        {
            var kind=settings.KindAt(i);var definition=EnemyCatalog.Get(kind);
            var variant=settings.VariantAt(i);var factor=RaidBalance.Factor(variant);
            var cost=definition.Power*factor*factor*settings.HealthMultiplier*settings.AttackMultiplier
                *Math.Sqrt(settings.SpeedMultiplier);
            if(legacyFallback&&i==0&&cost>budget)
            {
                variant=definition.Power<=budget?"regular":"weak";factor=RaidBalance.Factor(variant);
                cost=definition.Power*factor*factor;
            }
            if(settings.Mode=="automatic"&&spent+cost>budget+.0001)break;
            var healthFactor=variant=="boss"?factor*factor*.75:factor;
            var attackFactor=variant=="boss"?RaidBalance.Factor("strong"):factor;
            units.Add(new(kind,variant,(12+definition.Power*2)*healthFactor*settings.HealthMultiplier,
                (1.5+definition.Power*.4)*attackFactor*settings.AttackMultiplier,
                (kind==EnemyKind.Zombie?.55:.75)*settings.SpeedMultiplier,cost));
            spent+=cost;
        }
        if(settings.Mode=="automatic"&&units.Count==settings.Count&&spent>0&&budget>spent)
        {
            // At the actor cap, spend extra readiness on quality rather than another crowd.
            var quality=Math.Min(2,Math.Sqrt(budget/spent));
            for(var i=0;i<units.Count;i++)units[i]=units[i] with
                {Health=units[i].Health*quality,Attack=units[i].Attack*quality,Cost=units[i].Cost*quality*quality};
            spent=units.Sum(u=>u.Cost);
        }
        if(units.Count==0)throw new GameRuleException("raid.unaffordable","This composition exceeds the automatic budget. Reduce its strength or select manual mode.");
        var ratio=spent/Math.Max(1,readinessBudget);
        return new(readinessBudget,spent,units.Count,units.Sum(u=>u.Health),units.Sum(u=>u.Attack),
            ratio>1.25?"dangerous":ratio<.7?"weak":"comparable",units);
    }
}

/// <summary>Compact permanent launch receipt, retained after bulky battle details expire.</summary>
public sealed class RaidLaunchReceipt
{
    public Guid Id { get; set; }
    public Guid VillageId { get; set; }
    public Guid InitiatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public RaidSettings Settings { get; set; } = new();
}
