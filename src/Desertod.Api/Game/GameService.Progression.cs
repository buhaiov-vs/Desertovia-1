using Desertod.Domain.Catalog;
using Desertod.Domain.Rules;
using Desertod.Domain.Technologies;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;
public sealed record KnowledgeInvestment(string TechnologyId);
public sealed record MealRequest(ResourceKind Resource);
public sealed partial class GameService
{
    public async Task<VillageStateDto> InvestKnowledgeAsync(string slug,Guid actor,KnowledgeInvestment request,CancellationToken ct)=>
        await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>
        {
            var village=await LoadVillageAsync(db,slug,ct);await RequireRulerAsync(db,village,actor,ct);
            var now=time.GetUtcNow();var context=await LoadContextAsync(db,village,null,now,ct);
            TechnologyRules.InvestKnowledge(village,request.TechnologyId,context.Work.RecentCitizenCount,now);
            db.AuditActor=actor;db.AuditOperation="research-knowledge";
            await db.SaveChangesAsync(ct);changes.Mark(village.Id);
            return await BuildStateAsync(db,village,now,ct);
        },ct);

    public async Task<CitizenDto> EatMealAsync(string slug,Guid actor,MealRequest request,CancellationToken ct)
    {
        var key=await RequireCitizenAsync(slug,actor,ct);
        return await WithVillageAsync(key.VillageId,async db=>
        {
            await SyncHospitalAsync(db,key.VillageId,ct);
            var citizen=await db.Citizens.SingleAsync(c=>c.Id==key.Id,ct);
            var village=await db.Villages.SingleAsync(v=>v.Id==key.VillageId,ct);var now=time.GetUtcNow();
            PreparedMeals.Eat(citizen,village,request.Resource,now);
            db.AuditActor=actor;db.AuditOperation="prepared-meal";
            await db.SaveChangesAsync(ct);changes.Mark(village.Id);
            await notifier.CitizenUpdated(slug,citizen.ToSummaryDto(now));
            return citizen.ToDto(village,now);
        },ct);
    }
}
