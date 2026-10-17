using Desertod.Domain;
using Desertod.Domain.Combat;
using Desertod.Domain.Chat;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Desertod.Api.Game;
public sealed partial class GameService
{
    public async Task<bool> RequestRescueAsync(string slug, Guid actor, CancellationToken ct)
    {
        return await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>
        {
            var village=await LoadVillageAsync(db,slug,ct);var now=time.GetUtcNow();
            var tsar=village.OwnerAccountId==actor;
            var patient=tsar?null:await db.Citizens.SingleOrDefaultAsync(c=>c.VillageId==village.Id&&c.AccountId==actor&&c.Status==CitizenStatus.Active,ct);
            if(!tsar&&patient is null)throw new UnauthorizedAccessException();
            if(tsar?!(village.TsarInjuredUntil>now)||village.TsarHospitalizedAt is not null:!RescueRules.NeedsHelp(patient!,now))
                throw new GameRuleException("rescue.notNeeded","You do not need carrying to hospital.");
            var last=tsar?village.TsarRescueRequestedAt:patient!.RescueRequestedAt;
            if(last>now.AddMinutes(-1))return false;
            var name=patient?.Name??(await db.Accounts.SingleAsync(a=>a.Id==actor,ct)).DisplayName;
            var message=ChatMessage.Write(village.Id,actor,patient?.Id,name,"Нужна помощь: отнесите меня в больницу.",now);
            db.ChatMessages.Add(message);
            if(tsar)village.TsarRescueRequestedAt=now;else patient!.RescueRequestedAt=now;
            await db.SaveChangesAsync(ct);changes.Mark(village.Id);
            if(patient is not null)await notifier.CitizenUpdated(slug,patient.ToSummaryDto(now));
            await notifier.ChatMessageSent(slug,new(message.Id,message.AuthorName,message.CitizenId,message.IsTsar,message.Text,message.SentAt));
            return true;
        },ct);
    }
    public async Task<CitizenDto> RescueAsync(string slug,Guid actor,Guid patientId,CancellationToken ct)
    {
        var key=await RequireCitizenAsync(slug,actor,ct);
        return await WithVillageAsync(key.VillageId,async db=>
        {
            await SyncHospitalAsync(db,key.VillageId,ct);
            var village=await db.Villages.SingleAsync(v=>v.Id==key.VillageId,ct);
            var helper=await db.Citizens.SingleAsync(c=>c.Id==key.Id,ct);
            var now=time.GetUtcNow();var context=await LoadContextAsync(db,village,helper.Id,now,ct);
            Citizen? patient = null;
            if (patientId == village.OwnerAccountId)
            {
                if(helper.AccountId==village.OwnerAccountId)throw new GameRuleException("rescue.invalid","You cannot carry yourself.");
                if (!(village.TsarInjuredUntil>now)||village.TsarHospitalizedAt is not null)
                    throw new GameRuleException("rescue.notNeeded","The Tsar no longer needs carrying.");
                if (village.TsarRescueByCitizenId is not null)throw new GameRuleException("rescue.claimed","Someone is already carrying the Tsar.");
                // Общая проверка доступности работы и рук остаётся в обычном старте.
                var proxy = new Citizen { Id=patientId,AccountId=patientId,VillageId=village.Id,Name="Царь",InjuredUntil=village.TsarInjuredUntil,
                    WoundedAt=village.TsarWoundedAt,IdleSpot=village.TsarSpot };
                CycleEngine.StartRescue(helper,proxy,context.Work,now);
                helper.RescueJourney!.PatientIsTsar=true;
                village.TsarRescueByCitizenId=helper.Id;
            }
            else
            {
                patient=await db.Citizens.SingleOrDefaultAsync(c=>c.Id==patientId&&c.VillageId==key.VillageId&&c.Status==CitizenStatus.Active,ct)
                    ??throw new NotFoundException("citizen.notFound","Citizen not found.");
                CycleEngine.StartRescue(helper,patient,context.Work,now);
            }
            await db.SaveChangesAsync(ct);changes.Mark(village.Id);
            if(patient is not null)await notifier.CitizenUpdated(slug,patient.ToSummaryDto(now));
            await notifier.CitizenUpdated(slug,helper.ToSummaryDto(now));
            return helper.ToDto(village,now);
        },ct);
    }
    private async Task RescueSweepAsync(DesertodDbContext db,Village village,DateTimeOffset now,CancellationToken ct)
    {
        var people=await db.Citizens.Where(c=>c.VillageId==village.Id&&c.Status==CitizenStatus.Active).ToListAsync(ct);
        var tsarBefore=(village.TsarRescueByCitizenId,village.TsarSpot);
        var before=people.ToDictionary(p=>p.Id,p=>(p.HospitalizedAt,p.RescueByCitizenId,p.RescuePatientId,p.RecoveryPausedAt,p.CycleEndsAt,p.Spot,p.IdleSpot));
        foreach(var person in people)HospitalRules.Resume(person,now);
        foreach(var helper in people.Where(c=>c.RescuePatientId!=null))
        {
            if (helper.RescueJourney is {PatientIsTsar:true} journey)
            {
                if (village.TsarRescueByCitizenId != helper.Id || !(village.TsarInjuredUntil>now) || village.TsarHospitalizedAt is not null
                    || helper.CurrentTaskId!=RescueRules.TaskId || helper.RecoveryPausedAt is not null || CombatRules.IsInjured(helper,now))
                {
                    if (village.TsarRescueByCitizenId==helper.Id)
                    { if(now>=journey.PickupAt)village.TsarSpot=journey.PositionAt(now);village.TsarRescueByCitizenId=null; }
                    RescueRules.Cancel(helper,null,now,village);
                }
                continue;
            }
            var patient=people.SingleOrDefault(c=>c.Id==helper.RescuePatientId);
            var valid=patient is not null&&RescueRules.NeedsHelp(patient,now)&&patient.RescueByCitizenId==helper.Id
                && !CombatRules.IsInjured(helper,now) && helper.RecoveryPausedAt is null;
            if(!valid||helper.CurrentTaskId!=RescueRules.TaskId||helper.CycleEndsAt is null||helper.RescueJourney is {CompletedAt:not null})
                RescueRules.Cancel(helper,patient,now,village);
        }
        var helpers=people.ToDictionary(p=>p.Id);
        foreach(var patient in people.Where(c=>c.RescueByCitizenId is not null))
            if (!helpers.TryGetValue(patient.RescueByCitizenId!.Value,out var helper)||helper.RescuePatientId!=patient.Id)
                patient.RescueByCitizenId=null;
        if(village.TsarRescueByCitizenId is {} tsarHelper && (!helpers.TryGetValue(tsarHelper,out var carrier)||carrier.RescuePatientId!=village.OwnerAccountId))
            village.TsarRescueByCitizenId=null;
        var changed=people.Where(p=>before[p.Id]!=(p.HospitalizedAt,p.RescueByCitizenId,p.RescuePatientId,p.RecoveryPausedAt,p.CycleEndsAt,p.Spot,p.IdleSpot)).ToList();
        if(changed.Count>0 || tsarBefore!=(village.TsarRescueByCitizenId,village.TsarSpot))
        {
            await db.SaveChangesAsync(ct);changes.Mark(village.Id);
            foreach(var p in changed)await notifier.CitizenUpdated(village.Slug,p.ToSummaryDto(now));
        }
    }
}
