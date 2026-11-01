using Desertod.Domain.Catalog;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

public enum DenGuardState
{
    /// <summary>Walking its round of the den, or standing its post.</summary>
    Home,
    Chasing,

    /// <summary>Gave up the chase and walks home; it heals once there.</summary>
    Returning,
    Dead,
}

/// <summary>The citizen fighting at a den, as they come.</summary>
/// <param name="Damage">One blow: the citizen's fight power.</param>
/// <param name="Protection">Share of each blow the armor stops.</param>
/// <param name="Arrows">Shots left; null when the citizen holds no bow.</param>
/// <param name="Health">HP the hero brings into this fight (may be short while regenerating).</param>
/// <param name="MaximumHealth">Full pool for this citizen; the bar and resurrect use this.</param>
public sealed record DenFighter(Guid CitizenId, double Health, double MaximumHealth, double Damage, double Protection, int? Arrows);

public enum DenEventKind
{
    /// <summary>The hero struck a protector.</summary>
    Hit,

    /// <summary>A protector struck the hero.</summary>
    Struck,
    Slain,
    KnockedOut,
    Respawned,
}

public sealed record DenEvent(DenEventKind Kind, int Protector, double Amount = 0, bool Ranged = false);

public sealed class DenProtector
{
    public int Index { get; init; }
    public required DenGuard Guard { get; init; }
    public TilePoint Home { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Health { get; set; }
    public DenGuardState State { get; set; } = DenGuardState.Home;
    public DateTimeOffset? ReturnsAt { get; set; }
    public bool IsAlive => State != DenGuardState.Dead;
    internal double Cooldown { get; set; }
}

public sealed class DenHero
{
    public required DenFighter Fighter { get; init; }
    public Guid CitizenId => Fighter.CitizenId;
    public double X { get; set; }
    public double Y { get; set; }
    public double Health { get; set; }
    public int? Target { get; set; }
    public TilePoint? Destination { get; set; }
    public int? Arrows { get; set; }
    public int ArrowsSpent { get; set; }
    public int Kills { get; set; }
    public int Experience { get; set; }
    public Dictionary<ResourceKind, int> Loot { get; } = [];
    public bool KnockedOut { get; set; }
    public bool Shoots => Arrows is > 0;
    public double Range => Shoots ? DenFight.BowRange : DenFight.HandRange;
    internal double Cooldown { get; set; }
}

/// <summary>What a citizen takes home from a den fight.</summary>
/// <param name="Destination">Where the citizen was walking and did not reach; they go on there.</param>
/// <param name="Health">HP left when the fight ended; zero when knocked out.</param>
public sealed record DenOutcome(
    Guid CitizenId,
    TilePoint Position,
    TilePoint? Destination,
    bool KnockedOut,
    int InjuryMinutes,
    int Kills,
    int Experience,
    IReadOnlyList<ResourceAmount> Loot,
    int ArrowsSpent,
    double Health);

/// <summary>
/// One den with its protectors, run in memory by the server. Protectors mill a few steps around the den. One who sees the
/// hero calls the rest, and they chase together, but only inside the ring plus their sight: past that they turn back
/// and heal, so the pack cannot be led away from the den. A roaming foe still wakes within
/// <see cref="DenGuards.AggroRange"/> of where it stands and gives up at the edge of its claim. The hero walks where
/// the player sends them and strikes only the protector the player picked. A slain protector comes back after
/// <see cref="DenGuards.Respawn"/>. Pure: the same calls play out the same.
/// </summary>
public sealed class DenFight
{
    public const double HandRange = 1.2;
    public const double BowRange = 5;
    public const double HandSeconds = 1;
    public const double BowSeconds = 1.4;

    /// <summary>The citizen's normal walking pace; protectors chase faster, so only the leash lets a hero get away.</summary>
    public const double HeroSpeed = 1 / Exploration.SecondsPerTile;

    /// <summary>A walker this close to a point has reached it.</summary>
    private const double Reach = 0.05;

    /// <summary>Protectors keep this far apart, in tiles.</summary>
    private const double Spacing = 0.6;

    private readonly List<DenProtector> protectors;
    private readonly Func<int, int, bool> walkable;
    private DenOutcome? released;

    /// <param name="start">Where a roaming foe stands when the fight opens; it goes back there afterwards.</param>
    /// <param name="now">When the fight opens, so the den's protectors start on the round they are walking.</param>
    public DenFight(SpawnPoint point, Func<int, int, bool> walkable, TilePoint? start = null, DateTimeOffset now = default)
    {
        Point = point;
        this.walkable = walkable;
        var when = now == default ? DateTimeOffset.UnixEpoch : now;
        var guard = DenGuards.For(SpawnRings.Levels[point.Level - 1]);
        var count = Math.Max(1, point.Protectors);
        var homes = point.Roams && start is { } spot
            ? [spot]
            : Enumerable.Range(0, count).Select(index => DenGuards.PatrolAt(point.Center, index, count, point.Level, when)).ToList();
        protectors = homes
            .Select((home, index) => new DenProtector { Index = index, Guard = guard, Home = home, X = home.X, Y = home.Y, Health = guard.Health })
            .ToList();
        Anchor = point.Roams ? protectors[0].Home : point.Center;
    }

    public SpawnPoint Point { get; }

    /// <summary>The middle of the fight: the den, or where a roaming foe stood when it opened.</summary>
    public TilePoint Anchor { get; }
    public IReadOnlyList<DenProtector> Protectors => protectors;
    public DenHero? Hero { get; private set; }

    /// <summary>
    /// Past this distance from the den the protectors give up. A den's pack stops at the ring plus its sight,
    /// so it cannot be led off the spawn. A roaming foe gives up at half its claim.
    /// </summary>
    public double Leash => Point.Roams
        ? Point.Claim / 2.0
        : DenGuards.PostRadius(Point.Level) + DenGuards.SightOf(Point.Level);

    /// <summary>Nothing to run: no hero, every protector alive and at home.</summary>
    public bool IsResting => Hero is null && protectors.All(p => p.State == DenGuardState.Home);

    /// <summary>A hero at <paramref name="position"/> would wake this den.</summary>
    public bool Wakes(TilePoint position) => Hero is null && SeenByPack(position);

    /// <summary>The hero steps into the den's reach, walking on to <paramref name="destination"/> unless the player says otherwise.</summary>
    public bool Engage(DenFighter fighter, TilePoint position, TilePoint? destination)
    {
        if (Hero is not null)
        {
            return false;
        }

        Hero = new DenHero
        {
            Fighter = fighter,
            X = position.X,
            Y = position.Y,
            Health = fighter.Health,
            Arrows = fighter.Arrows,
            Destination = destination,
        };
        released = null;
        return true;
    }

    public bool Fights(Guid citizenId) => Hero?.CitizenId == citizenId;

    /// <summary>Walk to a spot; drops the target.</summary>
    public bool MoveTo(Guid citizenId, TilePoint spot)
    {
        if (!Fights(citizenId) || !walkable((int)Math.Floor(spot.X), (int)Math.Floor(spot.Y)))
        {
            return false;
        }

        Hero!.Target = null;
        Hero.Destination = spot;
        return true;
    }

    /// <summary>Pick a protector: the hero walks up to it and strikes until it falls.</summary>
    public bool Attack(Guid citizenId, int protector)
    {
        if (!Fights(citizenId) || protectors.ElementAtOrDefault(protector) is not { IsAlive: true })
        {
            return false;
        }

        Hero!.Target = protector;
        Hero.Destination = null;
        return true;
    }

    /// <summary>Developer tools: a hero who has fallen and is still in the fight stands up at full health.</summary>
    public bool Resurrect()
    {
        if (Hero is not { } hero || (hero.Health > 0 && !hero.KnockedOut))
        {
            return false;
        }

        hero.Health = hero.Fighter.MaximumHealth;
        hero.KnockedOut = false;
        return true;
    }

    /// <summary>The citizen was taken away (to the hospital): they leave the fight where they stand, unhurt.</summary>
    public void Withdraw(Guid citizenId)
    {
        if (Fights(citizenId))
        {
            Release(false, 0);
        }
    }

    /// <summary>The hero who left the fight since the last call, once.</summary>
    public DenOutcome? TakeReleased()
    {
        var outcome = released;
        released = null;
        return outcome;
    }

    public IReadOnlyList<DenEvent> Tick(double seconds, DateTimeOffset now, Random random)
    {
        var events = new List<DenEvent>();
        foreach (var protector in protectors.Where(p => p.State == DenGuardState.Dead && p.ReturnsAt <= now))
        {
            var home = HomeOf(protector, now);
            protector.State = DenGuardState.Home;
            protector.ReturnsAt = null;
            protector.Health = protector.Guard.Health;
            (protector.X, protector.Y) = (home.X, home.Y);
            events.Add(new DenEvent(DenEventKind.Respawned, protector.Index));
        }

        if (seconds <= 0)
        {
            return events;
        }

        if (Hero is { } hero)
        {
            hero.Cooldown = Math.Max(0, hero.Cooldown - seconds);
            ActHero(hero, seconds, now, random, events);
        }

        if (Hero is { } awake && SeenByPack(new TilePoint(awake.X, awake.Y)))
        {
            foreach (var protector in protectors.Where(p => p.State is DenGuardState.Home or DenGuardState.Returning))
            {
                protector.State = DenGuardState.Chasing;
            }
        }

        foreach (var protector in protectors.Where(p => p.IsAlive))
        {
            protector.Cooldown = Math.Max(0, protector.Cooldown - seconds);
            ActProtector(protector, seconds, now, events);
        }

        Spread();

        if (Hero is { } leaving && protectors.All(p => p.State != DenGuardState.Chasing)
            && (protectors.All(p => !p.IsAlive) || new TilePoint(leaving.X, leaving.Y).DistanceTo(Anchor) > Leash))
        {
            Release(false, 0);
        }

        return events;
    }

    private void ActHero(DenHero hero, double seconds, DateTimeOffset now, Random random, List<DenEvent> events)
    {
        var step = HeroSpeed * seconds;
        if (hero.Target is { } index)
        {
            var target = protectors[index];
            if (!target.IsAlive)
            {
                hero.Target = null;
            }
            else if (Distance(hero.X, hero.Y, target.X, target.Y) > hero.Range)
            {
                Walk(hero, target.X, target.Y, step);
                return;
            }
            else
            {
                if (hero.Cooldown <= 0)
                {
                    Strike(hero, target, now, random, events);
                }

                return;
            }
        }

        if (hero.Destination is { } destination && Walk(hero, destination.X, destination.Y, step))
        {
            hero.Destination = null;
        }
    }

    private void Strike(DenHero hero, DenProtector protector, DateTimeOffset now, Random random, List<DenEvent> events)
    {
        var ranged = hero.Shoots;
        if (ranged)
        {
            hero.Arrows--;
            hero.ArrowsSpent++;
        }

        var damage = hero.Fighter.Damage;
        protector.Health -= damage;
        hero.Cooldown = ranged ? BowSeconds : HandSeconds;
        events.Add(new DenEvent(DenEventKind.Hit, protector.Index, damage, ranged));
        if (protector.State is DenGuardState.Home or DenGuardState.Returning)
        {
            foreach (var other in protectors.Where(p => p.State is DenGuardState.Home or DenGuardState.Returning))
            {
                other.State = DenGuardState.Chasing;
            }
        }

        if (protector.Health > 0)
        {
            return;
        }

        protector.Health = 0;
        protector.State = DenGuardState.Dead;
        protector.ReturnsAt = now + DenGuards.Respawn;
        hero.Kills++;
        hero.Experience += protector.Guard.Experience;
        foreach (var loot in protector.Guard.Loot)
        {
            if (random.NextDouble() < loot.Chance)
            {
                hero.Loot[loot.Resource] = hero.Loot.GetValueOrDefault(loot.Resource) + random.Next(loot.Minimum, loot.Maximum + 1);
            }
        }

        if (hero.Target == protector.Index)
        {
            hero.Target = null;
        }

        events.Add(new DenEvent(DenEventKind.Slain, protector.Index));
    }

    private void ActProtector(DenProtector protector, double seconds, DateTimeOffset now, List<DenEvent> events)
    {
        var guard = protector.Guard;
        if (protector.State == DenGuardState.Chasing)
        {
            if (Hero is not { } hero || new TilePoint(hero.X, hero.Y).DistanceTo(Anchor) > Leash)
            {
                // A post that cannot walk was never led off, so it is home at once.
                if (guard.Speed <= 0)
                {
                    protector.State = DenGuardState.Home;
                    protector.Health = guard.Health;
                    protector.Cooldown = 0;
                }
                else
                {
                    protector.State = DenGuardState.Returning;
                }
            }
            else if (Distance(protector.X, protector.Y, hero.X, hero.Y) > guard.AttackRange)
            {
                if (guard.Speed > 0)
                {
                    Walk(protector, hero.X, hero.Y, guard.Speed * seconds);
                }

                return;
            }
            else
            {
                if (protector.Cooldown <= 0)
                {
                    var damage = guard.Damage * (1 - hero.Fighter.Protection);
                    hero.Health -= damage;
                    protector.Cooldown = guard.AttackSeconds;
                    events.Add(new DenEvent(DenEventKind.Struck, protector.Index, damage, guard.AttackRange > DenGuards.MeleeRange));
                    if (hero.Health <= 0)
                    {
                        hero.Health = 0;
                        events.Add(new DenEvent(DenEventKind.KnockedOut, protector.Index));
                        Release(true, guard.InjuryMinutes);
                    }
                }

                return;
            }
        }

        if (protector.State == DenGuardState.Returning)
        {
            var spot = HomeOf(protector, now);
            if (Distance(protector.X, protector.Y, spot.X, spot.Y) <= 0.35)
            {
                protector.X = spot.X;
                protector.Y = spot.Y;
                protector.State = DenGuardState.Home;
                protector.Health = guard.Health;
                protector.Cooldown = 0;
            }
            else
            {
                Walk(protector, spot.X, spot.Y, Math.Max(guard.Speed, DenGuards.PackSpeed) * DenGuards.ReturnSpeedMultiplier * seconds);
            }

            return;
        }

        if (protector.State == DenGuardState.Home && !Point.Roams)
        {
            var spot = HomeOf(protector, now);
            protector.X = spot.X;
            protector.Y = spot.Y;
        }
    }

    /// <summary>One living protector sees <paramref name="position"/>, or it is already inside the ring.</summary>
    private bool SeenByPack(TilePoint position)
    {
        if (!protectors.Any(p => p.IsAlive))
        {
            return false;
        }

        if (Point.Roams)
        {
            return position.DistanceTo(Anchor) <= DenGuards.AggroRange;
        }

        if (position.DistanceTo(Anchor) <= DenGuards.PostRadius(Point.Level))
        {
            return true;
        }

        var sight = DenGuards.SightOf(Point.Level);
        return protectors.Any(p => p.IsAlive && position.DistanceTo(new TilePoint(p.X, p.Y)) <= sight);
    }

    private TilePoint HomeOf(DenProtector protector, DateTimeOffset now) =>
        Point.Roams
            ? protector.Home
            : DenGuards.PatrolAt(Anchor, protector.Index, protectors.Count, Point.Level, now);

    private void Release(bool knockedOut, int injuryMinutes)
    {
        var hero = Hero!;
        hero.KnockedOut = knockedOut;
        released = new DenOutcome(
            hero.CitizenId,
            new TilePoint(hero.X, hero.Y),
            knockedOut ? null : hero.Destination,
            knockedOut,
            injuryMinutes,
            hero.Kills,
            hero.Experience,
            hero.Loot.Select(l => new ResourceAmount(l.Key, l.Value)).ToList(),
            hero.ArrowsSpent,
            knockedOut ? 0 : hero.Health);
        Hero = null;
        foreach (var protector in protectors.Where(p => p.State == DenGuardState.Chasing))
        {
            if (protector.Guard.Speed <= 0)
            {
                protector.State = DenGuardState.Home;
                protector.Health = protector.Guard.Health;
                protector.Cooldown = 0;
            }
            else
            {
                protector.State = DenGuardState.Returning;
            }
        }
    }

    /// <summary>Protectors standing on each other step apart; one that does not walk stays put.</summary>
    private void Spread()
    {
        var walking = protectors.Where(p => p.IsAlive && p.State != DenGuardState.Home).ToList();
        for (var i = 0; i < walking.Count; i++)
        {
            for (var j = i + 1; j < walking.Count; j++)
            {
                var (a, b) = (walking[i], walking[j]);
                var gap = Distance(a.X, a.Y, b.X, b.Y);
                if (gap >= Spacing)
                {
                    continue;
                }

                var (dx, dy) = gap > 1e-6 ? ((b.X - a.X) / gap, (b.Y - a.Y) / gap) : (Math.Cos(i + j), Math.Sin(i + j));
                var push = (Spacing - gap) / 2;
                if (a.Guard.Speed > 0) Place(a, a.X - dx * push, a.Y - dy * push);
                if (b.Guard.Speed > 0) Place(b, b.X + dx * push, b.Y + dy * push);
            }
        }
    }

    private bool Walk(DenHero hero, double toX, double toY, double distance)
    {
        var (x, y, there) = Step(hero.X, hero.Y, toX, toY, distance);
        if (walkable((int)Math.Floor(x), (int)Math.Floor(y)))
        {
            (hero.X, hero.Y) = (x, y);
        }

        return there;
    }

    private bool Walk(DenProtector protector, double toX, double toY, double distance)
    {
        var (x, y, there) = Step(protector.X, protector.Y, toX, toY, distance);
        Place(protector, x, y);
        return there;
    }

    private void Place(DenProtector protector, double x, double y)
    {
        if (!Point.Roams)
        {
            var dx = x - Anchor.X;
            var dy = y - Anchor.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance > Leash)
            {
                var scale = Leash / distance;
                x = Anchor.X + dx * scale;
                y = Anchor.Y + dy * scale;
            }
        }

        if (walkable((int)Math.Floor(x), (int)Math.Floor(y)))
        {
            (protector.X, protector.Y) = (x, y);
        }
    }

    private static (double X, double Y, bool There) Step(double fromX, double fromY, double toX, double toY, double distance)
    {
        var gap = Distance(fromX, fromY, toX, toY);
        if (gap <= distance + Reach)
        {
            return (toX, toY, true);
        }

        return (fromX + (toX - fromX) / gap * distance, fromY + (toY - fromY) / gap * distance, false);
    }

    private static double Distance(double x1, double y1, double x2, double y2) => Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
}
