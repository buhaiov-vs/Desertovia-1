using Desertod.Domain;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Desertod.Api.Game;
public sealed partial class GameService
{
    public async Task<CitizenDto> RescueAsync(string slug,Guid actor,Guid patientId,CancellationToken ct)
    {
        var key=await RequireCitizenAsync(slug,actor,ct);
        return await WithVillageAsync(key.VillageId,async db=>
        {
            await SyncHospitalAsync(db,key.VillageId,ct);
            var village=await db.Villages.SingleAsync(v=>v.Id==key.VillageId,ct);
            var helper=await db.Citizens.SingleAsync(c=>c.Id==key.Id,ct);
            var patient=await db.Citizens.SingleOrDefaultAsync(c=>c.Id==patientId&&c.VillageId==key.VillageId&&c.Status==CitizenStatus.Active,ct)
                ??throw new NotFoundException("citizen.notFound","Citizen not found.");
            var now=time.GetUtcNow();var context=await LoadContextAsync(db,village,helper.Id,now,ct);
            CycleEngine.StartRescue(helper,patient,context.Work,now);
            await db.SaveChangesAsync(ct);changes.Mark(village.Id);
            await notifier.CitizenUpdated(slug,patient.ToSummaryDto(now));await notifier.CitizenUpdated(slug,helper.ToSummaryDto(now));
            return helper.ToDto(village,now);
        },ct);
    }
    private async Task RescueSweepAsync(DesertodDbContext db,Village village,DateTimeOffset now,CancellationToken ct)
    {
        var people=await db.Citizens.Where(c=>c.VillageId==village.Id&&c.Status==CitizenStatus.Active).ToListAsync(ct);
        var before=people.ToDictionary(p=>p.Id,p=>(p.HospitalizedAt,p.RescueByCitizenId,p.RescuePatientId,p.RecoveryPausedAt,p.CycleEndsAt));
        foreach(var person in people)HospitalRules.Resume(person,now);
        foreach(var helper in people.Where(c=>c.RescuePatientId!=null))
        {
            var patient=people.SingleOrDefault(c=>c.Id==helper.RescuePatientId);
            var valid=patient is not null&&RescueRules.NeedsHelp(patient,now)&&patient.RescueByCitizenId==helper.Id;
            if(valid&&helper.CycleSettled&&helper.CurrentTaskId==RescueRules.TaskId)RescueRules.Admit(patient!,village,now);
            else if(!valid||helper.CurrentTaskId!=RescueRules.TaskId||helper.CycleEndsAt is null)
            {if(patient?.RescueByCitizenId==helper.Id)patient.RescueByCitizenId=null;helper.RescuePatientId=null;}
        }
        foreach(var patient in people.Where(c=>RescueRules.NeedsHelp(c,now)&&c.RescueByCitizenId is null))
            if(patient.WoundedAt is null||now-patient.WoundedAt>=TimeSpan.FromMinutes(5))RescueRules.Admit(patient,village,now);
        var changed=people.Where(p=>before[p.Id]!=(p.HospitalizedAt,p.RescueByCitizenId,p.RescuePatientId,p.RecoveryPausedAt,p.CycleEndsAt)).ToList();
        if(changed.Count>0)
        {
            await db.SaveChangesAsync(ct);changes.Mark(village.Id);
            foreach(var p in changed)await notifier.CitizenUpdated(village.Slug,p.ToSummaryDto(now));
        }
    }
}
