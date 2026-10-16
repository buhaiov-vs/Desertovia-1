using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;
public sealed partial class GameService
{
    public async Task<object> GetCombatDiagnosticsAsync(string slug,Guid actor,CancellationToken ct)
    {
        await using var db=await contextFactory.CreateDbContextAsync(ct);
        var village=await db.Villages.AsNoTracking().SingleOrDefaultAsync(v=>v.Slug==slug,ct)??throw new NotFoundException("village.missing","Village not found.");
        if(village.OwnerAccountId!=actor)throw new UnauthorizedAccessException();
        using var process=System.Diagnostics.Process.GetCurrentProcess();
        return new {release=configuration["Hosting:ReleaseSha"],capturedAt=time.GetUtcNow(),cpuMs=process.TotalProcessorTime.TotalMilliseconds,
            workingSetBytes=process.WorkingSet64,managedBytes=GC.GetTotalMemory(false),collections=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)},battles=CombatDiagnostics.Read(village.Id)};
    }

    public async Task<bool> RetireGuardDutiesAsync(CancellationToken ct)
    {
        var ids=GameCatalog.Tasks.Where(t=>t.Effect==TaskEffect.Guard).Select(t=>t.Id).ToArray();
        await using var lookup=await contextFactory.CreateDbContextAsync(ct);
        var retired=await lookup.Citizens.AsNoTracking().Where(c=>!c.CycleSettled&&ids.Contains(c.CurrentTaskId!))
            .Select(c=>new {c.Id,c.VillageId}).ToListAsync(ct);
        foreach(var group in retired.GroupBy(c=>c.VillageId))
        {
            foreach(var item in group)
                await WithVillageAsync(group.Key,async db=>{
                    var c=await db.Citizens.SingleOrDefaultAsync(c=>c.Id==item.Id,ct);var now=time.GetUtcNow();
                    if(c is null||c.CycleSettled||!RaidSimulation.CanFight(c,now)||!ids.Contains(c.CurrentTaskId))return false;
                    // The old fixed-price duty was already charged. Bank that one result through the normal receipt path.
                    if(c.CycleEndsAt>now)c.CycleEndsAt=now;
                    await db.SaveChangesAsync(ct);return true;
                },ct);
            await SettleVillageAsync(group.Key,group.Select(c=>c.Id).ToArray(),ct);
        }
        return retired.Count>0;
    }
}
