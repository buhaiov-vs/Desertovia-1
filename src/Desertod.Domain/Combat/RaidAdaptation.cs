using Desertod.Domain.Entities;

namespace Desertod.Domain.Combat;

public sealed class RaidAdaptation
{
    public double Coefficient { get; set; } = 1;
    public int EasyStreak { get; set; }
    public string Reason { get; set; } = "Начальная сложность";
    public List<RaidAssessment> Recent { get; set; } = [];
    public List<RaidParticipation> Participation { get; set; } = [];
}
public sealed class RaidParticipation
{
    public Guid CitizenId { get; set; }
    public int Encounters { get; set; }
}
public sealed class RaidAssessment
{
    public Guid BattleId { get; set; }
    public DateTimeOffset At { get; set; }
    public required string Result { get; set; }
    public double ContactSeconds { get; set; }
    public string Participants { get; set; } = "";
}
public static class RaidAdaptationRules
{
    public static void Record(Village village,Raid raid,RaidBattle battle,double healthPool,DateTimeOffset at)
    {
        var state=village.RaidAdaptation;
        if(battle.Settings is null||raid.Outcome==RaidOutcome.Cancelled||battle.ManualOverride||battle.FirstContactAt is null
            ||state.Recent.Any(r=>r.BattleId==battle.Id)||battle.Events.Any(e=>e.Kind=="adapted"))return;
        var seconds=Math.Max(1,((battle.LastContactAt??battle.FirstContactAt.Value)-battle.FirstContactAt.Value).TotalSeconds);
        var wall=battle.Damage.Where(d=>d.PlotId.StartsWith("wall:",StringComparison.Ordinal)).Sum(d=>d.Amount);
        var easy=raid.Killed>=battle.Units.Count*.8 && seconds<=90 && raid.Injured==0
            && battle.PlayerHealthLost<Math.Max(1,healthPool)*.15 && battle.RemediesUsed==0
            && battle.DamageDone<(battle.Settings?.PropertyLimit??180)*.25
            && wall<(battle.Settings?.WallLimit??300)*.35 && raid.Stolen.Count==0;
        var heavy=raid.Outcome==RaidOutcome.Lost;
        if(heavy)
        {
            state.Coefficient=Math.Max(.75,state.Coefficient*.82);state.EasyStreak=0;
            state.Reason="Тяжёлые потери: следующий бюджет уменьшен на 18% (нижняя граница 75%)";
        }
        else if(easy)
        {
            if(++state.EasyStreak>=2)
            {
                state.Coefficient=Math.Min(1.5,state.Coefficient*1.1);state.EasyStreak=0;
                state.Reason="Два лёгких отражения подряд: следующий бюджет +10% (верхняя граница 150%)";
            }
            else state.Reason="Первое лёгкое отражение: сложность сохранена, ожидается следующий результат";
        }
        else {state.EasyStreak=0;state.Reason="Сопоставимый бой: сложность сохранена";}
        var actors=battle.Fighters.Where(f=>!f.IsTsar&&(f.DamageDealt>0||f.DamageTaken>0||f.Wounded))
            .Select(f=>f.CitizenId.ToString("N")).Distinct().ToArray();
        state.Recent.Add(new(){BattleId=battle.Id,At=at,ContactSeconds=seconds,
            Result=heavy?"heavy":easy?"easy":"comparable",Participants=string.Join(',',actors.Select(id=>"+"+id).Concat((battle.Events.FirstOrDefault(e=>e.Kind=="readiness")?.Target??"").Split(',',StringSplitOptions.RemoveEmptyEntries).Except(actors).Select(id=>"-"+id)))});
        state.Recent=state.Recent.TakeLast(10).ToList();
        state.Participation=state.Recent.SelectMany(r=>r.Participants.Split(',',StringSplitOptions.RemoveEmptyEntries))
            .GroupBy(id=>id.TrimStart('+','-')).Select(g=>new RaidParticipation{CitizenId=Guid.ParseExact(g.Key,"N"),Encounters=g.Count(id=>!id.StartsWith('-'))}).ToList();
        battle.Event("adapted",state.Reason,at);
    }
}
