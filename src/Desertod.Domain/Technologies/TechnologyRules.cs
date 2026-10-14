using Desertod.Domain.Buildings;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.World;

namespace Desertod.Domain.Technologies;

public static class TechnologyRules
{
    public static bool Knows(Village village, string? technologyId) =>
        technologyId is null || village.Research.Any(r => r.TechnologyId == technologyId && r.ResearchedAt is not null);

    public static TechnologyProgress? ProgressOf(Village village, string technologyId) =>
        village.Research.SingleOrDefault(r => r.TechnologyId == technologyId);

    /// <summary>Not discovered yet, and every technology it builds on is.</summary>
    public static bool IsAvailable(Village village, TechnologyDefinition technology) =>
        !Knows(village, technology.Id) && technology.Requires.All(required => Knows(village, required));

    /// <summary>The latest era the village has discovered a technology of.</summary>
    public static Era EraOf(Village village) =>
        village.Research
            .Where(r => r.ResearchedAt is not null)
            .Select(r => TechnologyCatalog.Find(r.TechnologyId)?.Era ?? Era.Wild)
            .DefaultIfEmpty(Era.Wild)
            .Max();

    /// <summary>What the village researches now: the technology the last vote chose, until it is discovered.</summary>
    public static TechnologyDefinition? Current(Village village) =>
        village.ChosenTechnology is { } id && TechnologyCatalog.Find(id) is { } technology && IsAvailable(village, technology)
            ? technology
            : null;

    /// <summary>
    /// The Tsar or a Governor puts some available technologies to the vote. A poll needs two options, or one when only one is
    /// available. The village keeps researching the previous choice until the new vote closes.
    /// </summary>
    public static TechnologyPoll OpenPoll(Village village, IReadOnlyCollection<string> technologyIds, int activeCitizens, DateTimeOffset now)
    {
        if (village.Poll is { IsOpen: true })
        {
            throw new GameRuleException("poll.alreadyOpen", "A vote is already open. Wait for it to close or cancel it.");
        }

        if (activeCitizens == 0)
        {
            throw new GameRuleException("poll.noCitizens", "Nobody lives in the village yet to vote.");
        }

        var options = technologyIds.Distinct().ToList();
        foreach (var technologyId in options)
        {
            CheckAvailable(village, Find(technologyId));
        }

        var minimum = Math.Min(2, TechnologyCatalog.Technologies.Count(t => IsAvailable(village, t)));
        if (minimum == 0)
        {
            throw new GameRuleException("poll.nothingToChoose", "Every discovery within reach is already made.");
        }

        if (options.Count < minimum)
        {
            throw new GameRuleException("poll.tooFewOptions", $"Pick at least {minimum} discoveries to vote on.",
                new Dictionary<string, object> { ["minimum"] = minimum });
        }

        village.Poll = new TechnologyPoll
        {
            Options = TechnologyCatalog.Technologies.Select(t => t.Id).Where(options.Contains).ToList(),
            OpenedAt = now,
        };
        return village.Poll;
    }

    /// <summary>The Tsar or a Governor withdraws an open vote; the previous choice stays.</summary>
    public static void CancelPoll(Village village, DateTimeOffset now)
    {
        if (village.Poll is not { IsOpen: true } poll)
        {
            throw new GameRuleException("poll.notOpen", "No vote is open.");
        }

        poll.ClosedAt = now;
    }

    /// <summary>
    /// A citizen votes for one of the options, or changes the vote. The poll closes once more than half of the active
    /// citizens voted. Returns true if this vote closed it.
    /// </summary>
    public static bool CastVote(Village village, Citizen citizen, string technologyId, int activeCitizens, DateTimeOffset now)
    {
        if (village.Poll is not { IsOpen: true } poll)
        {
            throw new GameRuleException("poll.notOpen", "No vote is open.");
        }

        if (!poll.Options.Contains(technologyId))
        {
            throw new GameRuleException("poll.notAnOption", "This discovery is not on the ballot.",
                new Dictionary<string, object> { ["technology"] = technologyId });
        }

        if (poll.BallotOf(citizen.Id) is { } ballot)
        {
            ballot.TechnologyId = technologyId;
        }
        else
        {
            poll.Ballots.Add(new PollBallot { CitizenId = citizen.Id, TechnologyId = technologyId });
        }

        if (poll.Ballots.Count < TechnologyPoll.VotesNeeded(activeCitizens))
        {
            return false;
        }

        Close(village, poll, activeCitizens, now);
        return true;
    }

    /// <summary>The most voted option wins. Ties go to the one with more progress, then to the earlier one in the catalog.</summary>
    private static void Close(Village village, TechnologyPoll poll, int activeCitizens, DateTimeOffset now)
    {
        var winner = poll.Options
            .Select((id, index) => (id, index, votes: poll.VotesFor(id)))
            .OrderByDescending(x => x.votes)
            .ThenByDescending(x => ProgressOf(village, x.id)?.Done ?? 0)
            .ThenBy(x => x.index)
            .First().id;

        poll.ClosedAt = now;
        poll.Electorate = activeCitizens;
        poll.Winner = winner;
        village.ChosenTechnology = winner;
    }

    private static void CheckAvailable(Village village, TechnologyDefinition technology)
    {
        if (Knows(village, technology.Id))
        {
            throw new GameRuleException("technology.known", $"{technology.Name} is already discovered.", Arguments(technology));
        }

        var missing = technology.Requires.Where(required => !Knows(village, required)).ToList();
        if (missing.Count > 0)
        {
            throw new GameRuleException("technology.requires",
                $"{technology.Name} needs: {string.Join(", ", missing.Select(m => TechnologyCatalog.Get(m).Name))}.",
                new Dictionary<string, object> { ["technology"] = technology.Id, ["technologies"] = string.Join(",", missing) });
        }
    }

    /// <summary>Checks that a citizen can research the current technology now. Returns it and the plot of its building.</summary>
    public static (TechnologyDefinition Technology, string PlotId) CheckResearch(Village village, Citizen citizen)
    {
        var technology = Current(village)
                         ?? throw new GameRuleException("research.none", "No discovery is chosen. Ask the Tsar or a Governor to call a vote.");

        var plotId = PlotOf(village, technology.ResearchedAt)
                     ?? throw new GameRuleException("research.needsBuilding",
                         $"{technology.Name} is researched at a {BuildingCatalog.Get(technology.ResearchedAt).Name}. Ask the Tsar or a Governor to plan one.",
                         new Dictionary<string, object> { ["technology"] = technology.Id, ["building"] = technology.ResearchedAt.ToString() });

        var lacking = technology.Researcher.FirstOrDefault(r => citizen.Stats.Get(r.Stat) < r.Minimum);
        if (lacking is not null)
        {
            throw new GameRuleException("research.skillRequired",
                $"Researching {technology.Name} needs {lacking.Stat} {lacking.Minimum}.",
                new Dictionary<string, object>
                {
                    ["technology"] = technology.Id,
                    ["stat"] = lacking.Stat.ToString(),
                    ["level"] = lacking.Minimum,
                });
        }

        var progress = ProgressOf(village, technology.Id);
        if (progress is not null && Remaining(technology, progress) == 0 && progress.HasHand(citizen.Id))
        {
            throw new GameRuleException("research.needOthers",
                "The research is done, but more minds are needed. Someone who has not studied it yet must help.",
                Arguments(technology));
        }

        return (technology, plotId);
    }

    /// <summary>
    /// Adds a cycle of research. A technology is discovered when its work is done and enough different citizens took part.
    /// Returns the work added and whether this cycle discovered it.
    /// </summary>
    public static (int Amount, bool Discovered) Research(
        Village village,
        Citizen citizen,
        TechnologyDefinition technology,
        double factor,
        int recentCitizens,
        DateTimeOffset now)
    {
        if (Knows(village, technology.Id))
        {
            return (0, false);
        }

        var progress = ProgressOf(village, technology.Id);
        if (progress is null)
        {
            progress = new TechnologyProgress { TechnologyId = technology.Id };
            village.Research.Add(progress);
        }

        var newHand = !progress.HasHand(citizen.Id);
        var amount = Math.Min(YieldMath.Whole(factor), Remaining(technology, progress));
        if (amount == 0 && !newHand)
        {
            return (0, false);
        }

        progress.Done += amount;
        progress.AddHand(citizen.Id);

        var discovered = Remaining(technology, progress) == 0 && progress.Hands.Count >= CrewNeeded(technology, recentCitizens);
        if (discovered)
        {
            progress.ResearchedAt = now;
        }

        // Joining the crew counts as work even when nothing was left to study.
        return (Math.Max(amount, newHand ? 1 : 0), discovered);
    }

    public static int Remaining(TechnologyDefinition technology, TechnologyProgress progress) =>
        Math.Max(0, technology.Cycles - progress.Done);

    /// <summary>Scaled down for small streams, like the crew of a construction stage.</summary>
    public static int CrewNeeded(TechnologyDefinition technology, int recentCitizens) =>
        Math.Max(1, Math.Min(technology.Crew, recentCitizens));

    /// <summary>Product of the bonuses of every discovered technology for a task.</summary>
    public static double YieldMultiplier(Village village, string taskId) =>
        TechnologyCatalog.Technologies
            .Where(t => Knows(village, t.Id))
            .SelectMany(t => t.Bonuses)
            .Where(b => b.TaskId == taskId)
            .Aggregate(1.0, (product, bonus) => product * bonus.Multiplier);

    /// <summary>The building of this kind nearest the camp, where researchers gather.</summary>
    public static string? PlotOf(Village village, BuildingKind kind)
    {
        var camp = WorldMap.Places[WorldMap.Camp];
        return village.Structures.Where(s => s.Kind == kind).Select(s => s.PlotId)
            .Concat(village.Storages.Where(s => BuildingCatalog.ForStorage(s.Kind) == kind).Select(s => s.PlotId))
            .MinBy(plotId => WorldMap.Yard(plotId).DistanceTo(camp));
    }

    private static TechnologyDefinition Find(string technologyId) =>
        TechnologyCatalog.Find(technologyId)
        ?? throw new GameRuleException("technology.unknown", $"Unknown technology \"{technologyId}\".",
            new Dictionary<string, object> { ["technology"] = technologyId });

    private static Dictionary<string, object> Arguments(TechnologyDefinition technology) =>
        new() { ["technology"] = technology.Id };
}
