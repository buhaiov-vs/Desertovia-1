using Desertod.Domain.Rules;

namespace Desertod.Api.Tests;

/// <summary>
/// Real time plus an offset tests move forward, so a cycle or a raid that takes minutes is over at once.
/// Timers still tick in real time: the background workers keep polling as in production.
/// </summary>
public sealed class TestClock : TimeProvider
{
    private long offsetTicks = SkipFreeEnergy(System.GetUtcNow()).Ticks;

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + TimeSpan.FromTicks(Interlocked.Read(ref offsetTicks));

    /// <summary>Only forward: every test in the collection shares the clock, and time never runs back in the game.</summary>
    public void Advance(TimeSpan by)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(by, TimeSpan.Zero);
        Interlocked.Add(ref offsetTicks, by.Ticks);
    }

    /// <summary>Tests check energy spending, which a playtest window of free energy would hide.</summary>
    private static TimeSpan SkipFreeEnergy(DateTimeOffset now) =>
        GameRules.IsFreeEnergy(now) ? GameRules.FreeEnergyUntil - now : TimeSpan.Zero;
}
