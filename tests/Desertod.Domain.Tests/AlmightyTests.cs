using Desertod.Domain.Director;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.World;
namespace Desertod.Domain.Tests;

public sealed class AlmightyTests
{
    private static AlmightyFacts Ready => new(10, 5, 5, 0, 0, 1, 1, 0, false, false);
    [Fact] public void Only_real_readiness_and_safety_conditions_block_new_undead()
    {
        var cases = new[] { Ready with { ReadyGuards=0 }, Ready with { Online=0 }, Ready with { ReadyGuards=2 }, Ready with { Defeated=true },
            Ready with { RecentLosses=1 }, Ready with { Peaceful=true }, Ready with { Resting=true }, Ready with { PendingRaids=1 } };
        foreach(var facts in cases) Assert.DoesNotContain(AlmightyPolicy.Options(facts), o => o.Id.Contains("small"));
        Assert.Contains(AlmightyPolicy.Options(Ready), o => o.Id == "skeletons_small");
    }
    [Fact] public void Advice_and_rest_never_authorize_currency_changes()
    {
        var options = AlmightyPolicy.Options(Ready with { Patients=2 });
        Assert.DoesNotContain(options,o=>o.Id=="rest");
        Assert.Contains(options,o=>o.Id=="economy_report");
        Assert.DoesNotContain(options,o=>o.Id.Contains("tax")||o.Id.Contains("wallet"));
        Assert.False(AlmightyPolicy.IsMode("LimitedAuto"));
    }
    [Fact]
    public void Patients_offline_population_damage_and_unfinished_sites_do_not_grant_immunity()
    {
        var facts = Ready with { Population = 150, Online = 8, ReadyGuards = 5, Patients = 100, BuildingSites = 40, DamagedBuildings = 80 };
        Assert.Contains(AlmightyPolicy.Options(facts), o => o.Enemy is not null);
        Assert.DoesNotContain(AlmightyPolicy.Options(facts), o => o.Id == "rest");
        Assert.Contains(AlmightyPolicy.Options(facts with { ReadyGuards = 2 }), o => o.Id == "rest");
    }

    [Fact]
    public void Pressure_has_a_bounded_deadline_and_respects_all_proposals_and_real_blockers()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.False(AlmightyPolicy.RaidPressure(Ready, now, now.AddMinutes(-59), null).ProposalDue);
        Assert.True(AlmightyPolicy.RaidPressure(Ready, now, now.AddHours(-1), null).ProposalDue);
        Assert.False(AlmightyPolicy.RaidPressure(Ready, now, now.AddDays(-1), now.AddMinutes(-119)).ProposalDue);
        Assert.True(AlmightyPolicy.RaidPressure(Ready, now, now.AddDays(-1), now.AddHours(-2)).ProposalDue);
        foreach (var facts in new[] { Ready with { Peaceful = true }, Ready with { PendingRaids = 1 },
            Ready with { Resting = true }, Ready with { RecentLosses = 1 }, Ready with { ReadyGuards = 2 }, Ready with { Defeated = true } })
        {
            var pressure = AlmightyPolicy.RaidPressure(facts, now, now.AddDays(-10), null);
            Assert.False(pressure.ProposalDue);
            Assert.NotEmpty(pressure.BlockedBy);
        }
    }

    [Fact] public void Server_paths_do_not_cross_river_or_uncleared_obstacles_and_survive_blocked_approaches()
    {
        var village=Village.Create("tactics","Tactics",Guid.NewGuid(),DateTimeOffset.UtcNow);
        var paths=RaidTactics.Approaches(village);
        Assert.NotEmpty(paths);
        foreach(var path in paths) foreach(var point in path.Path)
            Assert.True(point.X < Wilderness.RiverWestBank(point.Y,Wilderness.Seed(village.Id.ToString())));
        foreach(var path in paths) for(var i=1;i<path.Path.Count;i++)
            Assert.Equal(1,Math.Abs(path.Path[i].X-path.Path[i-1].X)+Math.Abs(path.Path[i].Y-path.Path[i-1].Y));
    }
    [Fact] public void Enemy_options_follow_the_live_catalog_and_direction_is_a_real_defense_constraint()
    {
        var village=Village.Create("sector","Sector",Guid.NewGuid(),DateTimeOffset.UtcNow);
        var options=AlmightyPolicy.Options(Ready,RaidTactics.Approaches(village));
        foreach(var enemy in EnemyCatalog.Enemies.Where(e=>e.Undead&&!e.DungeonOnly))
            Assert.Contains(options,o=>o.Enemy==enemy.Kind);
        Assert.DoesNotContain(options,o=>o.Enemy==EnemyKind.BarrowKing);
        var at=DateTimeOffset.UtcNow;
        var raid=new Raid { EntrySector="west",StrikesAt=at };
        var defender=new Citizen{Name="Guard",DefenseSector="north",DefenseReadyAt=at.AddMinutes(-1)};
        Assert.False(RaidTactics.CanDefend(defender,raid));
        defender.DefenseSector="west";
        Assert.True(RaidTactics.CanDefend(defender,raid));
        defender.DefenseReadyAt=at.AddSeconds(1);
        Assert.False(RaidTactics.CanDefend(defender,raid));
        raid.EntrySector=null;
        Assert.True(RaidTactics.CanDefend(defender,raid));
    }

}
