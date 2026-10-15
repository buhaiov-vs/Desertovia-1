using Desertod.Domain;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Rules;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed record BattleOrder(Guid? TargetUnitId = null, string Mode = "intercept");
public sealed record GateOrder(bool Open);

public sealed partial class GameService
{
    private async Task PrepareBattleAsync(DesertodDbContext db,Village village,Raid raid,DateTimeOffset now,CancellationToken ct,string? sector=null)
    {
        var people=await db.Citizens.Where(c=>c.VillageId==village.Id&&c.Status==CitizenStatus.Active).ToListAsync(ct);
        var completed=await db.VillageProjects.Where(p=>p.VillageId==village.Id&&p.Status==ProjectStatus.Completed)
            .Select(p=>p.DefinitionId).ToListAsync(ct);
        db.RaidBattles.Add(RaidBalance.Prepare(village,raid,people,completed,
            people.Where(c=>presence.IsPresent(c.Id)).Select(c=>c.Id).ToHashSet(),now,sector));
    }

    private static RaidReport BattleReport(Raid raid,RaidBattle battle,IReadOnlyList<Citizen> people) => new(
        raid.Id,raid.Source,raid.Enemy,raid.Count,raid.Wave,raid.Waves,raid.StrikesAt,raid.Outcome==RaidOutcome.Repelled,
        raid.Killed,0,0,
        battle.Fighters.Where(f=>f.DamageDealt>0).Select(f=>new RaidDefenderReport(f.CitizenId,
            people.FirstOrDefault(c=>c.Id==f.CitizenId)?.Name??"",f.Wounded,0,[])).ToArray(),
        battle.Fighters.Where(f=>f.Wounded&&f.DamageDealt==0).Select(f=>new RaidVictimReport(f.CitizenId,
            people.FirstOrDefault(c=>c.Id==f.CitizenId)?.Name??"")).ToArray(),
        raid.Loot.Select(g=>new Desertod.Domain.Catalog.ResourceAmount(g.Kind,g.Amount)).ToArray(),
        raid.Stolen.Select(g=>new Desertod.Domain.Catalog.ResourceAmount(g.Kind,g.Amount)).ToArray(),
        battle.Damage.Select(d=>d.PlotId).ToArray(),0);

    public async Task SetBattleOrderAsync(string slug,Guid actor,Guid battleId,BattleOrder order,CancellationToken ct)
    {
        if(order.Mode is not ("hold" or "intercept"))throw new GameRuleException("defense.mode","Unknown defense mode.");
        var key=await RequireCitizenAsync(slug,actor,ct);
        await WithVillageAsync(key.VillageId,async db=>
        {
            var citizen=await db.Citizens.SingleAsync(c=>c.Id==key.Id,ct);
            var now=time.GetUtcNow();HospitalRules.RequireReady(citizen,now);
            if(RaidRules.GuardTask(citizen,now) is null)throw new GameRuleException("defense.guard","Start guard duty first.");
            var battle=await db.RaidBattles.SingleOrDefaultAsync(b=>b.Id==battleId&&b.VillageId==key.VillageId&&b.FinishedAt==null,ct)
                ??throw new GameRuleException("raid.finished","This battle has finished.");
            var fighter=battle.Fighters.SingleOrDefault(f=>f.CitizenId==citizen.Id)
                ??throw new GameRuleException("defense.arriving","The guard is arriving. Try again shortly.");
            if(fighter.Health<=0)throw new GameRuleException("defense.wounded","A wounded guard cannot attack.");
            if(order.TargetUnitId is {} target && !battle.Units.Any(u=>u.Id==target&&u.Alive))
                throw new GameRuleException("raid.targetGone","This enemy is no longer available.");
            fighter.Mode=order.Mode;fighter.TargetUnitId=order.TargetUnitId;battle.Revision++;
            await db.SaveChangesAsync(ct);changes.Mark(key.VillageId);return true;
        },ct);
    }

    public async Task SetGateAsync(string slug,Guid actor,GateOrder order,CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>
        {
            var village=await LoadVillageAsync(db,slug,ct);
            await RequireRulerAsync(db,village,actor,ct);
            var completed=await db.VillageProjects.Where(p=>p.VillageId==village.Id&&p.Status==ProjectStatus.Completed).Select(p=>p.DefinitionId).ToListAsync(ct);
            Fortifications.Ensure(village,completed);
            if(village.Fortifications.Count==0)throw new GameRuleException("defense.noWall","Build the palisade first.");
            foreach(var gate in village.Fortifications.Where(f=>f.Gate))gate.Open=order.Open;
            await db.SaveChangesAsync(ct);changes.Mark(village.Id);return true;
        },ct);
}
