using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Desertod.Domain;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed record RaidLaunchRequest(Guid RequestId,RaidSettings Settings);
public sealed record RaidPresetRequest(Guid? Id,string Name,RaidSettings Settings);
public sealed record RaidEditorView(IReadOnlyList<SavedRaidPreset> Presets,double Coefficient,string Reason);

public sealed partial class GameService
{
    private static void RequireRaidOwner(Village village,Guid actor)
    {
        if(village.OwnerAccountId!=actor)throw new UnauthorizedAccessException();
    }

    private async Task<T> ReadRaidEditorAsync<T>(string slug,Guid actor,Func<DesertodDbContext,Village,Task<T>> read,CancellationToken ct)
    {
        var villageId=await FindVillageIdAsync(slug,ct);
        using var lease=await AcquireLockAsync(villageId,ct);
        await using var db=await contextFactory.CreateDbContextAsync(ct);
        var v=await LoadVillageAsync(db,slug,ct);RequireRaidOwner(v,actor);
        // WithVillageAsync flushes accounting mutations even for a no-save action.
        // A preview must use a disposable read context and must never enter that path.
        return await read(db,v);
    }

    public async Task<RaidEditorView> GetRaidEditorAsync(string slug,Guid actor,CancellationToken ct) =>
        await ReadRaidEditorAsync(slug,actor,(db,v)=>
        {
            return Task.FromResult(new RaidEditorView(v.RaidPresets.Select(p=>new SavedRaidPreset
                {Id=p.Id,Name=p.Name,UpdatedAt=p.UpdatedAt,Settings=p.Settings.Copy()}).ToArray(),
                v.RaidAdaptation.Coefficient,v.RaidAdaptation.Reason));
        },ct);

    private async Task<RaidBattle> BuildConfiguredRaidAsync(DesertodDbContext db,Village v,Raid raid,RaidSettings settings,DateTimeOffset now,CancellationToken ct)
    {
        if(v.DefeatedAt is not null)throw new GameRuleException("village.defeated","Restore the royal castle before starting another raid.");
        var people=await db.Citizens.Where(c=>c.VillageId==v.Id&&c.Status==CitizenStatus.Active).ToListAsync(ct);
        var projects=await db.VillageProjects.Where(p=>p.VillageId==v.Id&&p.Status==ProjectStatus.Completed)
            .Select(p=>p.DefinitionId).ToListAsync(ct);
        return RaidBalance.Prepare(v,raid,people,projects,people.Where(c=>presence.IsPresent(c.Id)).Select(c=>c.Id).ToHashSet(),now,settings:settings);
    }

    public async Task<RaidPreview> PreviewRaidAsync(string slug,Guid actor,RaidSettings settings,CancellationToken ct) =>
        await ReadRaidEditorAsync(slug,actor,async (db,v)=>
        {
            settings.Validate(true);
            var now=time.GetUtcNow();
            var raid=new Raid{Enemy=settings.Enemy,Waves=settings.Waves,StrikesAt=now.AddSeconds(settings.WarningSeconds)};
            // No SaveChanges, event, payment, notification or model call in preview.
            var battle=await BuildConfiguredRaidAsync(db,v,raid,settings,now,ct);
            var people=await db.Citizens.Where(c=>c.VillageId==v.Id&&c.Status==CitizenStatus.Active).ToListAsync(ct);
            var budget=RaidBalance.Assess(v,people,people.Where(c=>presence.IsPresent(c.Id)).Select(c=>c.Id).ToHashSet(),now,settings.Waves).Budget;
            return RaidSettingsPlanner.Preview(battle.Settings!,budget,true);
        },ct);

    public async Task<VillageStateDto> LaunchRaidAsync(string slug,Guid actor,RaidLaunchRequest request,CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>
        {
            var v=await LoadVillageAsync(db,slug,ct);RequireRaidOwner(v,actor);
            if(request.RequestId==Guid.Empty)throw new GameRuleException("battle.requestId","A unique action identifier is required.");
            if(request.Settings is null)throw new GameRuleException("raid.settings","Raid settings are required.");
            request.Settings.Validate(true);
            var receipt=await db.RaidLaunchReceipts.SingleOrDefaultAsync(b=>b.VillageId==v.Id&&b.Id==request.RequestId,ct);
            if(receipt is not null)
            {
                if(JsonSerializer.Serialize(receipt.Settings)!=JsonSerializer.Serialize(request.Settings))
                    throw new GameRuleException("raid.requestConflict","This launch identifier was already used with different settings.");
                return await BuildStateAsync(db,v,time.GetUtcNow(),ct);
            }
            if(v.Raids.Any(r=>r.IsPending)||await db.RaidBattles.AnyAsync(b=>b.VillageId==v.Id&&b.FinishedAt==null,ct))
                throw new GameRuleException("raid.pending","Finish or cancel the existing raid first.");
            var now=time.GetUtcNow();
            for(var wave=1;wave<=request.Settings.Waves;wave++)
            {
                var hash=SHA256.HashData(Encoding.UTF8.GetBytes($"{v.Id:N}:{request.RequestId:N}:{wave}"));
                var raid=new Raid{Id=new Guid(hash.AsSpan(0,16)),Source=RaidSource.Undead,Enemy=request.Settings.Enemy,
                    Wave=wave,Waves=request.Settings.Waves,AnnouncedAt=now,
                    StrikesAt=now.AddSeconds(request.Settings.WarningSeconds+(wave-1)*request.Settings.WaveIntervalSeconds)};
                var battle=await BuildConfiguredRaidAsync(db,v,raid,request.Settings,now,ct);
                battle.EventId=request.RequestId;battle.InitiatedBy=actor;
                battle.Event("announced",request.Settings.Mode,now);
                v.Raids.Add(raid);db.RaidBattles.Add(battle);
            }
            db.RaidLaunchReceipts.Add(new(){Id=request.RequestId,VillageId=v.Id,InitiatedBy=actor,CreatedAt=now,Settings=request.Settings.Copy()});
            RaidRules.UpdateNextRaidAt(v);db.AuditActor=actor;db.AuditOperation="raid-editor-launch";
            await db.SaveChangesAsync(ct);changes.Mark(v.Id);
            return await BuildStateAsync(db,v,now,ct);
        },ct);

    public async Task<RaidEditorView> SaveRaidPresetAsync(string slug,Guid actor,RaidPresetRequest request,CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug,ct),async db=>
        {
            var v=await LoadVillageAsync(db,slug,ct);RequireRaidOwner(v,actor);request.Settings.Validate(true);
            var name=request.Name?.Trim();
            if(string.IsNullOrEmpty(name)||name.Length>48)throw new GameRuleException("raid.presetName","Name the preset (1–48 characters).");
            var preset=request.Id is {} id?v.RaidPresets.FirstOrDefault(p=>p.Id==id):null;
            if(request.Id is not null&&preset is null)throw new GameRuleException("raid.presetGone","This preset no longer exists.");
            if(preset is null)
            {
                if(v.RaidPresets.Count>=8)throw new GameRuleException("raid.presetLimit","At most eight custom presets can be saved.");
                preset=new(){Name=name};v.RaidPresets.Add(preset);
            }
            preset.Name=name;preset.Settings=request.Settings.Copy();preset.UpdatedAt=time.GetUtcNow();
            db.AuditActor=actor;db.AuditOperation="raid-preset-save";
            await db.SaveChangesAsync(ct);
            return new RaidEditorView(v.RaidPresets,v.RaidAdaptation.Coefficient,v.RaidAdaptation.Reason);
        },ct);
}
