using Desertod.Domain.Combat;
using Desertod.Domain.World;

namespace Desertod.Domain.Tests;

public class DenFightTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly TilePoint Camp = WorldMap.Places[WorldMap.Camp];

    private static DenFight Den(int level) =>
        new(SpawnRings.Points(1).First(point => point.Level == level), Camp, (_, _) => true);

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
        Assert.False(den.Wakes(Beside(den, DenGuards.AggroRange + 1)));
        Assert.True(den.Wakes(Beside(den, DenGuards.AggroRange - 1)));

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
            Assert.True(new TilePoint(p.X, p.Y).DistanceTo(p.Home) < 0.1);
        });
    }

    [Fact]
    public void A_HeroWalkingPast_KeepsTheirWay_AndOutrunsThePack()
    {
        var den = Den(2);
        var hero = Guid.NewGuid();
        var destination = Beside(den, -(den.Leash + 10));
        den.Engage(new DenFighter(hero, 48, 4, 0, null), Beside(den, 5), destination);
        var at = Now;
        for (var step = 0; step < 600 && den.Hero is not null; step++)
        {
            at = at.AddSeconds(0.1);
            den.Tick(0.1, at, new Random(step));
        }

        var outcome = den.TakeReleased();
        Assert.NotNull(outcome);
        Assert.False(outcome.KnockedOut);
        Assert.Equal(destination, outcome.Destination);
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
}
