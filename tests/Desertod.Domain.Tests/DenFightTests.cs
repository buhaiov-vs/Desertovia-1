using Desertod.Domain;
using Desertod.Domain.Combat;
using Desertod.Domain.World;

namespace Desertod.Domain.Tests;

public class DenFightTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly TilePoint Camp = WorldMap.Places[WorldMap.Camp];

    private static DenFight Den(int level) =>
        new(SpawnRings.Points(1).First(point => point.Level == level), (_, _) => true, now: Now);

    /// <summary>A fresh citizen: every stat at 0, bare hands, 32 health.</summary>
    private static DenFighter Fresh(Guid id) => new(id, 32, 2, 0, null);

    /// <summary>Every stat at 10, guarding at 10, a sword with full sharpening and a shield.</summary>
    private static DenFighter Strongest(Guid id) => new(id, 72, 24, 0.4, null);

    private static TilePoint Beside(DenFight den, double distance) => new(den.Point.Center.X + distance, den.Point.Center.Y);

    /// <summary>Ticks at a tenth of a second; the hero always goes for the nearest living protector.</summary>
    private static DateTimeOffset Fight(DenFight den, Guid hero, DateTimeOffset start, double seconds)
    {
        var at = start;
        for (var step = 0; step < seconds * 10 && den.Hero is not null; step++)
        {
            if (den.Hero.Target is null && den.Protectors.Where(p => p.IsAlive).MinBy(p => new TilePoint(p.X, p.Y).DistanceTo(new(den.Hero.X, den.Hero.Y))) is { } next)
                den.Attack(hero, next.Index);
            at = at.AddSeconds(0.1);
            den.Tick(0.1, at, new Random(step));
        }

        return at;
    }

    [Fact]
    public void Protectors_WakeForAHeroNearTheDen_AndStrike()
    {
        var den = Den(1);
        Assert.False(den.Wakes(Beside(den, den.Leash + 2)));
        Assert.True(den.Wakes(Beside(den, 1)));

        var hero = Guid.NewGuid();
        Assert.True(den.Engage(Fresh(hero), Beside(den, 5), null));
        var at = Now;
        var struck = false;
        for (var step = 0; step < 50 && !struck; step++)
        {
            at = at.AddSeconds(0.1);
            struck = den.Tick(0.1, at, new Random(step)).Any(e => e.Kind == DenEventKind.Struck);
        }

        Assert.True(struck);
        Assert.All(den.Protectors, p => Assert.Equal(DenGuardState.Chasing, p.State));
        Assert.True(den.Hero!.Health < 32);
    }

    [Fact]
    public void Every_Den_HasEightProtectors_EachAsStrongAsOneOfItsPack()
    {
        foreach (var level in SpawnRings.Levels)
        {
            var den = Den(level.Level);
            Assert.Equal(SpawnRings.ProtectorsPerDen, den.Protectors.Count);
            Assert.All(den.Protectors, p => Assert.Equal(Math.Round(DenGuards.DenHealth(level.Level) / level.Protectors), p.Guard.Health));
        }
    }

    [Fact]
    public void A_FreshCitizen_SlaysSomeSlimes_ButFallsAtTheDen()
    {
        var den = Den(1);
        var hero = Guid.NewGuid();
        den.Engage(Fresh(hero), Beside(den, 5), null);
        Fight(den, hero, Now, 120);

        var outcome = den.TakeReleased();
        Assert.NotNull(outcome);
        Assert.True(outcome.KnockedOut);
        Assert.InRange(outcome.Kills, 1, SpawnRings.ProtectorsPerDen - 1);
        Assert.True(outcome.Experience > 0);
    }

    [Fact]
    public void The_StrongestCitizen_ClearsTheSlimeDen()
    {
        var den = Den(1);
        var hero = Guid.NewGuid();
        den.Engage(Strongest(hero), Beside(den, 5), null);
        Fight(den, hero, Now, 120);

        var outcome = den.TakeReleased();
        Assert.NotNull(outcome);
        Assert.False(outcome.KnockedOut);
        Assert.Equal(SpawnRings.ProtectorsPerDen, outcome.Kills);
        Assert.All(den.Protectors, p => Assert.Equal(DenGuardState.Dead, p.State));
    }

    [Fact]
    public void A_FallenCitizen_LiesWhereTheyFell_AndDoesNotWalkHome()
    {
        var village = TestWorld.NewVillage();
        var citizen = TestWorld.NewCitizen(village);
        var home = WorldMap.Places[WorldMap.Camp];
        var fallen = new TilePoint(home.X + 40, home.Y + 12);
        citizen.Spot = home;
        citizen.IdleSpot = home;
        citizen.SetOut(home, village, Now);
        var outcome = new DenOutcome(citizen.Id, fallen, home, true, 30, 1, 4, [], 0);

        DenRules.Settle(citizen, village, outcome, Now);

        Assert.Equal(fallen, citizen.IdleSpot);
        Assert.Equal(fallen, citizen.Spot);
        Assert.Null(citizen.Trip);
        Assert.True(CombatRules.IsInjured(citizen, Now));
        Assert.Equal("citizen.injured", Assert.Throws<GameRuleException>(() => citizen.GoTo(home, village, Now)).Code);
        Assert.Equal(fallen, citizen.IdleSpot);
    }

    [Fact]
    public void A_FreshCitizen_FallsAtTheBoars_AndIsWounded()
    {
        var den = Den(6);
        var hero = Guid.NewGuid();
        den.Engage(Fresh(hero), Beside(den, 5), null);
        Fight(den, hero, Now, 300);

        var outcome = den.TakeReleased();
        Assert.NotNull(outcome);
        Assert.True(outcome.KnockedOut);
        Assert.Equal(20 + 2 * 5, outcome.InjuryMinutes);
        Assert.Null(outcome.Destination);
        Assert.All(den.Protectors.Where(p => p.IsAlive), p => Assert.NotEqual(DenGuardState.Chasing, p.State));
    }

    [Fact]
    public void Eight_Liches_AreTooManyForTheStrongestCitizenAlone()
    {
        var den = Den(30);
        var hero = Guid.NewGuid();
        den.Engage(Strongest(hero), Beside(den, 4), null);
        Fight(den, hero, Now, 600);
        var outcome = den.TakeReleased()!;
        Assert.True(outcome.KnockedOut);
        Assert.True(outcome.Kills < SpawnRings.ProtectorsPerDen);
    }

    [Fact]
    public void Protectors_GiveUpPastTheClaim_WalkHome_AndHeal()
    {
        var den = Den(1);
        var hero = Guid.NewGuid();
        den.Engage(Fresh(hero), Beside(den, 4), null);
        var at = Now;
        for (var step = 0; step < 20; step++)
        {
            at = at.AddSeconds(0.1);
            den.Tick(0.1, at, new Random(step));
        }

        den.Attack(hero, 0);
        for (var step = 0; step < 15; step++)
        {
            at = at.AddSeconds(0.1);
            den.Tick(0.1, at, new Random(step));
        }

        Assert.True(den.Protectors[0].Health < den.Protectors[0].Guard.Health);
        var away = Beside(den, den.Leash + 5);
        Assert.True(den.MoveTo(hero, away));
        for (var step = 0; step < 400 && den.Hero is not null; step++)
        {
            at = at.AddSeconds(0.1);
            den.Tick(0.1, at, new Random(step));
        }

        var outcome = den.TakeReleased();
        Assert.NotNull(outcome);
        Assert.False(outcome.KnockedOut);
        Assert.True(outcome.Position.DistanceTo(den.Point.Center) > den.Leash);

        for (var step = 0; step < 400 && !den.IsResting; step++)
        {
            at = at.AddSeconds(0.1);
            den.Tick(0.1, at, new Random(step));
        }

        Assert.True(den.IsResting);
        Assert.All(den.Protectors, p =>
        {
            Assert.Equal(p.Guard.Health, p.Health);
            Assert.InRange(new TilePoint(p.X, p.Y).DistanceTo(den.Anchor), DenGuards.PostRadius(den.Point.Level) - 0.2, DenGuards.PostRadius(den.Point.Level) + 0.2);
        });
    }

    [Fact]
    public void A_HeroWalkingPast_KeepsTheirWay_AndThePackStaysByTheDen()
    {
        var den = Den(2);
        var hero = Guid.NewGuid();
        var destination = Beside(den, -(den.Leash + 10));
        den.Engage(new DenFighter(hero, 80, 4, 0, null), Beside(den, 1), destination);
        var at = Now;
        var strayed = false;
        for (var step = 0; step < 800 && den.Hero is not null; step++)
        {
            at = at.AddSeconds(0.1);
            den.Tick(0.1, at, new Random(step));
            if (den.Protectors.Any(p => p.IsAlive && new TilePoint(p.X, p.Y).DistanceTo(den.Anchor) > den.Leash + 0.05))
            {
                strayed = true;
            }
        }

        var outcome = den.TakeReleased();
        Assert.NotNull(outcome);
        Assert.False(strayed);
        Assert.False(outcome.KnockedOut);
        Assert.Equal(destination, outcome.Destination);
        Assert.True(outcome.Position.DistanceTo(den.Point.Center) > den.Leash);
        Assert.All(den.Protectors, p => Assert.Equal(p.Guard.Health, p.Health));
    }

    [Fact]
    public void A_HeroWhoRuns_DealsNoDamage_EvenInReach()
    {
        var den = Den(1);
        var hero = Guid.NewGuid();
        var home = den.Protectors[0].Home;
        den.Engage(Strongest(hero), home, new TilePoint(home.X + 30, home.Y));
        var at = Now;
        for (var step = 0; step < 20; step++)
        {
            at = at.AddSeconds(0.1);
            var events = den.Tick(0.1, at, new Random(step));
            Assert.DoesNotContain(events, e => e.Kind == DenEventKind.Hit);
        }

        Assert.Null(den.Hero!.Target);
        Assert.All(den.Protectors, p => Assert.Equal(p.Guard.Health, p.Health));
        Assert.Contains(den.Protectors, p => p.State == DenGuardState.Chasing);
    }

    [Fact]
    public void A_ChosenProtector_IsTheOnlyOneStruck()
    {
        var den = Den(1);
        var hero = Guid.NewGuid();
        den.Engage(Strongest(hero), den.Protectors[3].Home, null);
        Assert.True(den.Attack(hero, 3));
        var at = Now;
        var hit = false;
        for (var step = 0; step < 20 && !hit; step++)
        {
            at = at.AddSeconds(0.1);
            hit = den.Tick(0.1, at, new Random(step)).Any(e => e.Kind == DenEventKind.Hit && e.Protector == 3);
        }

        Assert.True(hit);
        Assert.True(den.Protectors[3].Health < den.Protectors[3].Guard.Health);
        Assert.All(den.Protectors.Where(p => p.Index != 3), p => Assert.Equal(p.Guard.Health, p.Health));
    }

    [Fact]
    public void A_SlainProtector_ReturnsAfterHalfAnHour()
    {
        var den = Den(1);
        var hero = Guid.NewGuid();
        den.Engage(Strongest(hero), Beside(den, 5), null);
        var end = Fight(den, hero, Now, 120);
        Assert.NotNull(den.TakeReleased());
        Assert.All(den.Protectors, p => Assert.Equal(DenGuardState.Dead, p.State));
        Assert.False(den.Wakes(Beside(den, 1)));

        den.Tick(0.1, end + DenGuards.Respawn - TimeSpan.FromMinutes(1), new Random(1));
        Assert.All(den.Protectors, p => Assert.Equal(DenGuardState.Dead, p.State));
        var events = den.Tick(0.1, end + DenGuards.Respawn, new Random(1));
        Assert.Equal(SpawnRings.ProtectorsPerDen, events.Count(e => e.Kind == DenEventKind.Respawned));
        Assert.True(den.IsResting);
        Assert.True(den.Wakes(Beside(den, 1)));
    }

    [Fact]
    public void Every_Level_HasProtectorsThatFight_AndGrowStronger()
    {
        DenGuard? previous = null;
        foreach (var level in SpawnRings.Levels)
        {
            var guard = DenGuards.For(level);
            Assert.True(guard.Health > 0 && guard.Damage > 0 && guard.AttackSeconds > 0, level.Name);
            if (previous is not null)
            {
                Assert.True(DenGuards.DenHealth(level.Level) > DenGuards.DenHealth(previous.Level));
                Assert.True(DenGuards.DenDamagePerSecond(level.Level) > DenGuards.DenDamagePerSecond(previous.Level));
            }

            previous = guard;
        }
    }

    [Fact]
    public void Protectors_WalkTheRing_AndAPostThatCannotWalkStays()
    {
        var den = Den(1);
        var start = den.Protectors.Select(p => new TilePoint(p.X, p.Y)).ToList();
        Assert.All(start, spot => Assert.Equal(DenGuards.PatrolRadius, spot.DistanceTo(den.Anchor), 5));
        den.Tick(1, Now.AddSeconds(3), new Random(1));
        Assert.All(den.Protectors, p => Assert.Equal(DenGuardState.Home, p.State));
        Assert.Contains(den.Protectors, p => new TilePoint(p.X, p.Y).DistanceTo(start[p.Index]) > 1);
        Assert.All(den.Protectors, p => Assert.Equal(DenGuards.PatrolRadius, new TilePoint(p.X, p.Y).DistanceTo(den.Anchor), 5));

        var plant = Den(14);
        var post = new TilePoint(plant.Protectors[0].X, plant.Protectors[0].Y);
        plant.Tick(5, Now.AddSeconds(20), new Random(1));
        Assert.Equal(post, new TilePoint(plant.Protectors[0].X, plant.Protectors[0].Y));
        Assert.Equal(DenGuards.PostRadius(14), post.DistanceTo(plant.Anchor), 5);
    }

    [Fact]
    public void Every_Approach_MeetsAProtector_BeforeTheSpots()
    {
        var center = new TilePoint(400.5, 250.5);
        foreach (var level in new[] { 1, 14, 30 })
        {
            var radius = DenGuards.PostRadius(level);
            var sight = DenGuards.SightOf(level);
            Assert.True(radius > 2, "the ring stands outside the spots");
            Assert.True(radius * Math.Sin(Math.PI / SpawnRings.ProtectorsPerDen) <= sight);
            for (var lap = 0; lap < 8; lap++)
            {
                var now = Now.AddSeconds(lap * DenGuards.PatrolSeconds / 8);
                for (var step = 0; step < 64; step++)
                {
                    var angle = step * Math.PI * 2 / 64;
                    var at = new TilePoint(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);
                    var nearest = Enumerable.Range(0, SpawnRings.ProtectorsPerDen)
                        .Min(index => at.DistanceTo(DenGuards.PatrolAt(center, index, SpawnRings.ProtectorsPerDen, level, now)));
                    Assert.True(nearest <= sight);
                }
            }
        }
    }

    [Fact]
    public void One_Protector_SeeingTheHero_CallsThePack_AndThePackDoesNotLeave()
    {
        var den = Den(1);
        var guard = den.Protectors[0];
        var dx = guard.X - den.Anchor.X;
        var dy = guard.Y - den.Anchor.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var heroAt = new TilePoint(
            den.Anchor.X + dx / length * (length + DenGuards.Sight - 0.4),
            den.Anchor.Y + dy / length * (length + DenGuards.Sight - 0.4));
        Assert.True(heroAt.DistanceTo(new TilePoint(guard.X, guard.Y)) < DenGuards.Sight);
        Assert.All(
            den.Protectors.Where(p => p.Index != 0),
            other => Assert.True(heroAt.DistanceTo(new TilePoint(other.X, other.Y)) > DenGuards.Sight));
        Assert.True(heroAt.DistanceTo(den.Anchor) > DenGuards.PatrolRadius);

        var hero = Guid.NewGuid();
        Assert.True(den.Engage(Fresh(hero), heroAt, null));
        den.Tick(0.1, Now, new Random(1));
        Assert.All(den.Protectors, p => Assert.Equal(DenGuardState.Chasing, p.State));

        var away = new TilePoint(
            den.Anchor.X + dx / length * (den.Leash + 40),
            den.Anchor.Y + dy / length * (den.Leash + 40));
        Assert.True(den.MoveTo(hero, away));
        var at = Now;
        for (var step = 0; step < 800 && den.Hero is not null; step++)
        {
            at = at.AddSeconds(0.1);
            den.Tick(0.1, at, new Random(step));
            Assert.All(
                den.Protectors.Where(p => p.IsAlive),
                p => Assert.True(new TilePoint(p.X, p.Y).DistanceTo(den.Anchor) <= den.Leash + 0.05));
        }

        var outcome = den.TakeReleased();
        Assert.NotNull(outcome);
        Assert.False(outcome.KnockedOut);
        Assert.True(outcome.Position.DistanceTo(den.Anchor) > den.Leash);
    }
}
