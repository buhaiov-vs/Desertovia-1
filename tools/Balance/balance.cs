#:project ../../src/Desertod.Domain/Desertod.Domain.csproj
// Prints pacing tables from the live catalogs: dotnet run tools/Balance/balance.cs
using System.Globalization;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Projects;
using Desertod.Domain.Quests;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Technologies;
using Desertod.Domain.World;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
const double Reaction = 5;
const double SiteWalk = 15;
var camp = WorldMap.Places[WorldMap.Camp];

double WalkSeconds(TaskDefinition t) => t.Effect switch
{
    TaskEffect.Resources when t.Outputs.Count > 0 => WorldMap.Walk(WorldMap.Place(t.Workplace), camp).TotalSeconds,
    TaskEffect.Expedition => WorldMap.Walk(WorldMap.Place(t.Workplace), camp).TotalSeconds,
    _ => 0,
};
double CycleSeconds(TaskDefinition t) => t.WorkSeconds + WalkSeconds(t) + Reaction;
TaskDefinition Task(string id) => GameCatalog.FindTask(id)!;

int StatCumulative(int level) => Enumerable.Range(0, level).Sum(Progression.ExperienceToNext);
int SkillCumulative(int level) => Enumerable.Range(0, level).Sum(SkillRules.ExperienceToNext);
int CyclesFor(int experience) => (int)Math.Ceiling(experience / (double)GameRules.BaseExperiencePerCycle);
double Factor(double stat) => YieldMath.StatFactor(stat);
var tasksPerDay = Energy.RegenerationPerHour(0) * 24 / GameRules.EnergyCostPerCycle;

Console.WriteLine("## A. Task length (empty village, goods dropped at the camp, 5 s to renew)\n");
Console.WriteLine("| Task | Work s | Walk s | Task s | Tasks/hour |");
Console.WriteLine("|---|---|---|---|---|");
foreach (var id in new[] { "chop-wood", "quarry-stone", "forage", "cut-reeds", "hunt", "track-game", "hunt-birds", "measure-river", "dig-cave", "tend-fire", "saw-planks", "keep-watch", "carry-to-storage", "deliver-wood", "build" })
{
    var t = Task(id);
    Console.WriteLine($"| {t.Name} | {t.WorkSeconds} | {WalkSeconds(t)} | {CycleSeconds(t)} | {3600 / CycleSeconds(t):0.0} |");
}

Console.WriteLine($"| Construction labor | {GameRules.CraftSeconds} | ~{SiteWalk} | {GameRules.CraftSeconds + SiteWalk + Reaction} | {3600 / (GameRules.CraftSeconds + SiteWalk + Reaction):0.0} |");
Console.WriteLine($"| Construction haul | {BuildingCatalog.HaulBaseTime.TotalSeconds} | ~{SiteWalk} | {BuildingCatalog.HaulBaseTime.TotalSeconds + SiteWalk + Reaction} | {3600 / (BuildingCatalog.HaulBaseTime.TotalSeconds + SiteWalk + Reaction):0.0} |");
var gatherCycle = new[] { "chop-wood", "quarry-stone", "forage", "cut-reeds" }.Average(id => CycleSeconds(Task(id)));
Console.WriteLine($"\nTypical gathering task: {gatherCycle:0} s = {3600 / gatherCycle:0.0} tasks per hour. A day of energy: {tasksPerDay:0} tasks.\n");

Console.WriteLine("## B. Levels from zero (10 experience per full task to the skill and the main stat, 3 to the side stat)\n");
Console.WriteLine("| Level | Skill XP | Skill tasks | Skill days of energy | Stat XP | Stat tasks (main stat) |");
Console.WriteLine("|---|---|---|---|---|---|");
for (var level = 1; level <= 10; level++)
{
    var skillTasks = CyclesFor(SkillCumulative(level));
    Console.WriteLine($"| {level} | {SkillCumulative(level)} | {skillTasks} | {skillTasks / tasksPerDay:0.0} | {StatCumulative(level)} | {CyclesFor(StatCumulative(level))} |");
}

var spreadCap = 5 * StatCumulative(6);
var specialistCap = 3 * StatCumulative(10);
Console.WriteLine($"\nTalent cap ({GameRules.TalentCap} points): {spreadCap} XP as 6 in every stat = {spreadCap / 13.0 / tasksPerDay:0.0} days; " +
                  $"{specialistCap} XP as 10/10/10 = {specialistCap / 13.0 / tasksPerDay:0.0} days (13 stat XP per full task).");

Console.WriteLine("\n## C. Energy: full-experience tasks per day\n");
var trees = GameRules.UnderTreesRegeneration;
var leanTo = BuildingCatalog.Get(BuildingKind.LeanTo).ShelterQuality;
Console.WriteLine($"| Endurance | Pool | Under the trees (× {trees}) | Lean-to (× {leanTo}) | Hut (× 1) |");
Console.WriteLine("|---|---|---|---|---|");
foreach (var endurance in new[] { 0, 4, 10 })
{
    var perDay = Energy.RegenerationPerHour(endurance) * 24;
    Console.WriteLine($"| {endurance} | {Energy.Maximum(endurance):0} | {perDay * trees:0} | {perDay * leanTo:0} | {perDay:0} |");
}

// D. Calendar simulation: a citizen gathering for one craft with Endurance as the side stat.
var milestones = new (string Name, int Cycles)[]
{
    ("Journeyman (skill 3)", CyclesFor(SkillCumulative(3))),
    ("Stat 6", CyclesFor(StatCumulative(6))),
    ("Master (skill 7)", CyclesFor(SkillCumulative(7))),
    ("Stat 10", CyclesFor(StatCumulative(10))),
    ("Skill 10", CyclesFor(SkillCumulative(10))),
};
IEnumerable<(double Start, double Hours)> Block(double start, double hours) => [(start, hours)];
var visits = Enumerable.Range(0, 32).Select(i => (7 + i * 0.5, gatherCycle / 3600)).ToList();
var profiles = new (string Name, Func<int, IEnumerable<(double Start, double Hours)>> Sessions)[]
{
    ("One 3 h stream a week", d => d % 7 == 0 ? Block(19, 3) : []),
    ("Three 3 h streams a week", d => d % 7 is 0 or 2 or 4 ? Block(19, 3) : []),
    ("A task every 30 min, 7:00–23:00", _ => visits),
    ("1 h every day", _ => Block(20, 1)),
    ("3 h every day", _ => Block(19, 3)),
    ("8 h every day", _ => Block(12, 8)),
    ("Bot, 24 h a day", _ => Block(0, 24)),
};

Dictionary<string, (int Day, double Hours)> Simulate(Func<int, IEnumerable<(double Start, double Hours)>> sessions, double shelter, int days = 120)
{
    var result = new Dictionary<string, (int, double)>();
    var enduranceXp = 0;
    int Endurance() { var l = 0; while (l < 10 && enduranceXp >= StatCumulative(l + 1)) l++; return l; }
    var energy = Energy.Maximum(0);
    var full = 0;
    var played = 0.0;
    var time = 0.0;
    var step = gatherCycle / 3600;
    void Regen(double until)
    {
        var e = Endurance();
        energy = Math.Min(Energy.Maximum(e), energy + (until - time) * Energy.RegenerationPerHour(e) * shelter);
        time = until;
    }

    for (var day = 0; day < days; day++)
    {
        foreach (var (start, hours) in sessions(day))
        {
            Regen(day * 24 + start);
            for (var t = 0.0; t + step <= hours + 1e-9; t += step)
            {
                if (energy >= 1)
                {
                    energy -= 1;
                    full++;
                    enduranceXp += 3;
                    foreach (var m in milestones.Where(m => m.Cycles == full))
                    {
                        result[m.Name] = (day + 1, played + step);
                    }
                }

                played += step;
                Regen(time + step);
            }
        }

        Regen((day + 1) * 24.0);
    }

    return result;
}

foreach (var (label, shelter) in new[] { ("under the trees", trees), ("in a hut", 1.0) })
{
    Console.WriteLine($"\n## D. Calendar day (hours played) to reach a level, sleeping {label}\n");
    Console.WriteLine("| Player | " + string.Join(" | ", milestones.Select(m => $"{m.Name}, {m.Cycles} tasks")) + " |");
    Console.WriteLine("|---|" + string.Concat(milestones.Select(_ => "---|")));
    foreach (var p in profiles)
    {
        var r = Simulate(p.Sessions, shelter);
        Console.WriteLine($"| {p.Name} | " + string.Join(" | ", milestones.Select(m =>
            r.TryGetValue(m.Name, out var v) ? $"day {v.Day} ({v.Hours:0.#} h)" : "> 120 days")) + " |");
    }
}

Console.WriteLine("\n## E. Discoveries: full tasks one fresh citizen needs to qualify as a researcher\n");
Console.WriteLine("| Discovery | Era | Needs | Tasks | Days of energy | Research tasks × crew |");
Console.WriteLine("|---|---|---|---|---|---|");
foreach (var tech in TechnologyCatalog.Technologies)
{
    var reqs = tech.Researcher.OrderByDescending(r => r.Minimum).ToList();
    var first = CyclesFor(StatCumulative(reqs[0].Minimum));
    var extra = reqs.Count > 1 ? Math.Max(0, CyclesFor(StatCumulative(reqs[1].Minimum) - 3 * first)) : 0;
    var cycles = first + extra;
    Console.WriteLine($"| {tech.Name} | {tech.Era} | {string.Join(", ", reqs.Select(r => $"{r.Stat} {r.Minimum}"))} | {cycles} | {cycles / tasksPerDay:0.0} | {tech.Cycles} × {tech.Crew} |");
}

// F. Buildings: person-tasks of gathering, hauling and labor.
var gatherTask = new Dictionary<ResourceKind, string>
{
    [ResourceKind.Wood] = "chop-wood", [ResourceKind.Stone] = "quarry-stone", [ResourceKind.Reeds] = "cut-reeds",
    [ResourceKind.Leather] = "hunt", [ResourceKind.Food] = "forage", [ResourceKind.Gold] = GoldVein.DigTask,
};

double GatherPerCycle(ResourceKind kind, double stat)
{
    if (kind == ResourceKind.Leather) return 1;
    if (kind == ResourceKind.Planks) return YieldMath.Whole(2 * Factor(stat));
    var t = Task(gatherTask[kind]);
    return YieldMath.Whole(t.Outputs.First(o => o.Resource == kind).Amount * Factor(stat));
}

double GatherSeconds(ResourceKind kind) =>
    kind == ResourceKind.Planks ? CycleSeconds(Task("saw-planks")) : CycleSeconds(Task(gatherTask[kind]));

(double Cycles, double Hours) GatherCost(ResourceKind kind, int amount, double stat)
{
    var cycles = Math.Ceiling(amount / GatherPerCycle(kind, stat));
    var seconds = cycles * GatherSeconds(kind);
    if (kind == ResourceKind.Planks)
    {
        var wood = GatherCost(ResourceKind.Wood, amount, stat);
        cycles += wood.Cycles;
        seconds += wood.Hours * 3600;
    }

    return (cycles, seconds / 3600);
}

(double Cycles, double Hours, int Crew) BuildingCost(BuildingDefinition b, double stat)
{
    double cycles = 0, seconds = 0;
    var siteFactor = Factor(stat) / GameRules.StatFactorBase;
    foreach (var stage in b.Stages)
    {
        foreach (var job in stage.Jobs)
        {
            var def = BuildingCatalog.FindJob(job.JobId)!;
            var per = YieldMath.Whole(def.PerCycle * siteFactor);
            var n = Math.Ceiling(job.Amount / (double)per);
            cycles += n;
            seconds += n * ((def.Kind == JobKind.Haul ? BuildingCatalog.HaulBaseTime.TotalSeconds : GameRules.CraftSeconds) + SiteWalk + Reaction);
            if (def.Kind == JobKind.Haul)
            {
                var g = GatherCost(def.Resource!.Value, job.Amount, stat);
                cycles += g.Cycles;
                seconds += g.Hours * 3600;
            }
        }
    }

    return (cycles, seconds / 3600, b.Stages.Max(s => s.Crew));
}

Console.WriteLine("\n## F. Buildings: person-tasks and person-hours including gathering the material (first copy)\n");
Console.WriteLine("| Building | Crew | Stats 0: tasks | Stats 0: hours | Stats 3: tasks | Stats 3: hours |");
Console.WriteLine("|---|---|---|---|---|---|");
foreach (var b in BuildingCatalog.Buildings.Where(b => b.Buildable))
{
    var low = BuildingCost(b, 0);
    var mid = BuildingCost(b, 2.4);
    Console.WriteLine($"| {b.Name} | {low.Crew} | {low.Cycles} | {low.Hours:0.0} | {mid.Cycles} | {mid.Hours:0.0} |");
}

var deliverTask = new Dictionary<ResourceKind, string>
{
    [ResourceKind.Wood] = "deliver-wood", [ResourceKind.Stone] = "deliver-stone", [ResourceKind.Reeds] = "deliver-reeds",
    [ResourceKind.Gold] = "deliver-gold",
};

(double Cycles, double Hours) ProjectCost(ProjectDefinition p, double stat)
{
    double cycles = 0, seconds = 0;
    foreach (var m in p.Materials)
    {
        var g = GatherCost(m.Resource, m.Amount, stat);
        var deliver = Task(deliverTask[m.Resource]);
        var per = YieldMath.Whole(deliver.Outputs[0].Amount * Factor(stat));
        var n = Math.Ceiling(m.Amount / (double)per);
        cycles += g.Cycles + n;
        seconds += g.Hours * 3600 + n * CycleSeconds(deliver);
    }

    var work = p.SurveyCycles + p.BuildCycles;
    cycles += work;
    seconds += work * CycleSeconds(Task("build"));
    return (cycles, seconds / 3600);
}

Console.WriteLine("\n## G. Story chain: person-tasks and person-hours\n");
Console.WriteLine("| Step | Stats 0: tasks | Stats 0: hours | Stats 3: tasks | Stats 3: hours |");
Console.WriteLine("|---|---|---|---|---|");
double total0 = 0, total3 = 0, totalH0 = 0, totalH3 = 0;
void Row(string name, (double C, double H) a, (double C, double H) b)
{
    total0 += a.C; total3 += b.C; totalH0 += a.H; totalH3 += b.H;
    Console.WriteLine($"| {name} | {a.C} | {a.H:0.0} | {b.C} | {b.H:0.0} |");
}

(double, double) Building(BuildingKind kind, double stat) => BuildingCost(BuildingCatalog.Get(kind), stat) is var c ? (c.Cycles, c.Hours) : default;
(double, double) Repeat(string taskId, int times) => (times, times * CycleSeconds(Task(taskId)) / 3600);

Row("Fire (5 × tend the fire)", Repeat("tend-fire", StoryQuests.FireCycles), Repeat("tend-fire", StoryQuests.FireCycles));
Row("Shelter (lean-to)", Building(BuildingKind.LeanTo, 0), Building(BuildingKind.LeanTo, 2.4));
Row("Stores (20 food)", GatherCost(ResourceKind.Food, StoryQuests.StoresFood, 0), GatherCost(ResourceKind.Food, StoryQuests.StoresFood, 2.4));
Row("First house (hut)", Building(BuildingKind.Hut, 0), Building(BuildingKind.Hut, 2.4));
foreach (var id in new[] { ProjectCatalog.TsarMonument, ProjectCatalog.FishingPier, ProjectCatalog.Palisade, ProjectCatalog.TsarBridge })
{
    var p = ProjectCatalog.Find(id)!;
    Row(p.Name, ProjectCost(p, 0), ProjectCost(p, 2.4));
}

Row("Monks' gold (20 digs)", Repeat(GoldVein.DigTask, StoryQuests.GoldDigs), Repeat(GoldVein.DigTask, StoryQuests.GoldDigs));
foreach (var id in new[] { ProjectCatalog.BellTower, ProjectCatalog.GreatGranary })
{
    var p = ProjectCatalog.Find(id)!;
    Row(p.Name, ProjectCost(p, 0), ProjectCost(p, 2.4));
}

Console.WriteLine($"| **Total** | {total0} | {totalH0:0} | {total3} | {totalH3:0} |");

// H. Buildings needed to research the whole tree.
var needed = new HashSet<BuildingKind>();
void Need(BuildingKind k) { if (needed.Add(k)) foreach (var r in BuildingCatalog.Get(k).Requires) Need(r); }
foreach (var t in TechnologyCatalog.Technologies) Need(t.ResearchedAt);
var tree0 = needed.Select(k => BuildingCost(BuildingCatalog.Get(k), 0)).ToList();
var tree3 = needed.Select(k => BuildingCost(BuildingCatalog.Get(k), 2.4)).ToList();
var researchTasks = TechnologyCatalog.Technologies.Sum(t => t.Cycles);
var research = researchTasks * (GameRules.CraftSeconds + SiteWalk + Reaction) / 3600;
Console.WriteLine("\n## H. Whole discovery tree\n");
Console.WriteLine($"Buildings needed: {string.Join(", ", needed)}");
Console.WriteLine($"Those buildings: {tree0.Sum(c => c.Cycles)} tasks / {tree0.Sum(c => c.Hours):0} person-hours at stats 0, " +
                  $"{tree3.Sum(c => c.Cycles)} tasks / {tree3.Sum(c => c.Hours):0} at stats 3. Research itself: {researchTasks} tasks / {research:0.0} person-hours.");

Console.WriteLine("\n## I. Village calendar: days of work when every citizen spends a day of energy\n");
var content0 = total0 + tree0.Sum(c => c.Cycles) + researchTasks;
var content3 = total3 + tree3.Sum(c => c.Cycles) + researchTasks;
Console.WriteLine("| Citizens | Story and discovery tree, stats 0 | Stats 3 |");
Console.WriteLine("|---|---|---|");
foreach (var citizens in new[] { 3, 5, 10, 20 })
{
    Console.WriteLine($"| {citizens} | {content0 / (citizens * tasksPerDay):0.0} days | {content3 / (citizens * tasksPerDay):0.0} days |");
}
