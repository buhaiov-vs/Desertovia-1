using Desertod.Domain.Combat;

namespace Desertod.Api.Game;

/// <summary>Bounded combat-only timing history. No player identities, messages, coordinates or credentials.</summary>
public static class CombatDiagnostics
{
    private const int MaximumBattles=32, MaximumSamples=120;
    private static readonly object Gate=new();
    private static readonly Dictionary<Guid,Trace> Traces=[];
    private sealed class Trace(Guid village,Guid battle)
    {
        public Guid Village=village, Battle=battle;
        public DateTimeOffset LastAt, LoggedAt;
        public Queue<Sample> Samples=[];
        public double LockMs, SaveMs, SendMs;
    }
    public sealed record Sample(DateTimeOffset At,int Units,int Fighters,int Steps,double BacklogMs,double SimulationMs,
        int PathSearches,int PathNodes,int BlockedPaths,double LockMs,double SaveMs,double SendMs);
    public sealed record Report(Guid BattleId,DateTimeOffset LastAt,IReadOnlyList<Sample> Samples);

    public static void Record(ILogger logger,Guid village,Guid battle,DateTimeOffset now,int units,int fighters,RaidTickMetrics metric)
    {
        object? summary=null;
        lock(Gate)
        {
            if(!Traces.TryGetValue(battle,out var trace))
            {
                if(Traces.Count>=MaximumBattles)Traces.Remove(Traces.MinBy(x=>x.Value.LastAt).Key);
                Traces[battle]=trace=new(village,battle);
            }
            trace.LastAt=now;
            trace.Samples.Enqueue(new(now,units,fighters,metric.Steps,metric.BacklogMs,metric.ElapsedMs,
                metric.PathSearches,metric.PathNodes,metric.BlockedPaths,trace.LockMs,trace.SaveMs,trace.SendMs));
            while(trace.Samples.Count>MaximumSamples)trace.Samples.Dequeue();
            if(now-trace.LoggedAt>=TimeSpan.FromSeconds(15))
            {
                trace.LoggedAt=now;var recent=trace.Samples.Where(s=>s.At>=now.AddSeconds(-15)).ToArray();
                summary=new {battle,units,fighters,ticks=recent.Length,simulationMaxMs=recent.Max(s=>s.SimulationMs),
                    backlogMaxMs=recent.Max(s=>s.BacklogMs),pathSearches=recent.Sum(s=>s.PathSearches),pathNodes=recent.Sum(s=>s.PathNodes),
                    blockedPaths=recent.Sum(s=>s.BlockedPaths),lockMaxMs=recent.Max(s=>s.LockMs),saveMaxMs=recent.Max(s=>s.SaveMs),sendMaxMs=recent.Max(s=>s.SendMs)};
            }
        }
        if(summary is not null)
        {
            using var process=System.Diagnostics.Process.GetCurrentProcess();
            logger.LogInformation("Combat performance {CombatPerformance}",System.Text.Json.JsonSerializer.Serialize(new {
                timing=summary,cpuMs=process.TotalProcessorTime.TotalMilliseconds,workingSetBytes=process.WorkingSet64,
                managedBytes=GC.GetTotalMemory(false),gen0=GC.CollectionCount(0),gen1=GC.CollectionCount(1),gen2=GC.CollectionCount(2)}));
        }
    }
    public static void Stage(Guid village,string stage,double ms)
    {
        if(!double.IsFinite(ms)||ms<0)return;
        lock(Gate)foreach(var trace in Traces.Values.Where(t=>t.Village==village))
        {if(stage=="lock")trace.LockMs=ms;else if(stage=="save")trace.SaveMs=ms;else if(stage=="send")trace.SendMs=ms;}
    }
    public static void Sent(Guid battle,double ms)
    {lock(Gate)if(Traces.TryGetValue(battle,out var trace))trace.SendMs=ms;}
    public static IReadOnlyList<Report> Read(Guid village)
    {lock(Gate)return Traces.Values.Where(t=>t.Village==village).OrderByDescending(t=>t.LastAt).Select(t=>new Report(t.Battle,t.LastAt,t.Samples.ToArray())).ToArray();}
}
