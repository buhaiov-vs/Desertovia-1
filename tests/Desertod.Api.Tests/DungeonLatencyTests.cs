using System.Reflection;
using Desertod.Api.Game;
using Desertod.Domain.Catalog;
using Desertod.Domain.Dungeons;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DungeonLatencyTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);

    private static Task Tick(DungeonWorker worker, LiveDungeon live, CancellationToken ct = default) =>
        (Task)typeof(DungeonWorker).GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(worker, [live, 0.1, ct])!;

    private static Dictionary<Guid, Task> Settlements(DungeonWorker worker) =>
        (Dictionary<Guid, Task>)typeof(DungeonWorker).GetField("settlements", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(worker)!;

    private async Task<(GameService Game, DungeonRuntime Runtime, DungeonWorker Worker, LiveDungeon Live)> Setup()
    {
        var village = await players.FoundVillageAsync("Responsive dungeon");
        await using var db = await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var stored = await db.Villages.SingleAsync(v => v.Slug == village.Slug);
        var game = factory.Services.GetRequiredService<GameService>();
        var runtime = new DungeonRuntime();
        var fighter = new DungeonFighter(Guid.NewGuid(), stored.OwnerAccountId, "Survivor", HeroClass.Brawler, 10000, 1, 0, null, false);
        var second = fighter with { Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), Name = "Leaving" };
        var live = runtime.Start(new DungeonStart(Guid.NewGuid(), stored.Id, village.Slug,
            DungeonCatalog.Parse("test", "Test", ["TTTTTTTTTT", "E......r.T", "TTTTTTTTTT"], TimeSpan.FromMinutes(10)),
            1, [fighter, second], [], new Dictionary<Guid, Guid> { [fighter.Id] = fighter.AccountId, [second.Id] = second.AccountId }));
        var worker = ActivatorUtilities.CreateInstance<DungeonWorker>(factory.Services, runtime, NullLogger<DungeonWorker>.Instance);
        return (game, runtime, worker, live);
    }

    [Fact]
    public async Task Slow_settlement_does_not_stop_survivor_movement_or_queue_duplicate_rewards()
    {
        var s = await Setup();
        using var worker = s.Worker;
        using var cancellation = new CancellationTokenSource();
        var locks = (VillageLocks)typeof(GameService).GetField("locks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(s.Game)!;
        using var blockedVillage = await locks.AcquireAsync(s.Live.VillageId, default);
        var heroes = s.Live.Simulation.Heroes;
        s.Live.Simulation.WithdrawToHospital(heroes[1].Id);
        Assert.True(s.Live.Simulation.MoveTo(heroes[0].Id, 4.5, 1.5));
        var x = heroes[0].X;
        var first = Tick(worker, s.Live, cancellation.Token);
        try
        {
            // The database gate remains held for this entire assertion. An awaited settlement
            // cannot finish, but the simulation and its next snapshots must keep advancing.
            await first.WaitAsync(TimeSpan.FromSeconds(2));
            var pending = Assert.Single(Settlements(worker)).Value;
            Assert.False(pending.IsCompleted);
            for (var i = 0; i < 4; i++) await Tick(worker, s.Live, cancellation.Token).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(heroes[0].X > x);
            Assert.Equal(0.5, s.Live.LastSnapshot!.Elapsed);
            Assert.Same(pending, Assert.Single(Settlements(worker)).Value);
            Assert.Single(s.Live.Settled);
        }
        finally
        {
            cancellation.Cancel();
            await first;
            await Task.WhenAll(Settlements(worker).Values);
        }
        Assert.Empty(s.Live.Settled); // Cancelled persistence leaves the exit eligible for a retry.
        Assert.Same(s.Live, s.Runtime.Find(s.Live.RunId));
    }

    [Fact]
    public async Task Failed_final_transaction_keeps_the_run_and_its_rewards_for_retry()
    {
        var s = await Setup();
        using var worker = s.Worker;
        foreach (var hero in s.Live.Simulation.Heroes) s.Live.Simulation.WithdrawToHospital(hero.Id);
        await Tick(worker, s.Live);
        var first = Assert.Single(Settlements(worker)).Value;
        await first.WaitAsync(TimeSpan.FromSeconds(5)); // This isolated runtime run deliberately has no database row.
        Assert.True(s.Live.Simulation.IsOver);
        Assert.Same(s.Live, s.Runtime.Find(s.Live.RunId));
        Assert.Empty(s.Live.Settled);
        await Tick(worker, s.Live);
        var retry = Assert.Single(Settlements(worker)).Value;
        Assert.NotSame(first, retry);
        await retry.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(s.Live, s.Runtime.Find(s.Live.RunId));
        Assert.Empty(s.Live.Settled);
    }
}
