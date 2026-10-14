namespace Desertod.Domain.Technologies;

/// <summary>
/// A vote the Tsar or a Governor calls on the next discovery. Citizens pick one of the options; it closes once more than half of the
/// active citizens voted. The last poll is kept after closing so everyone can see the results.
/// </summary>
public sealed class TechnologyPoll
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Technologies put to the vote.</summary>
    public List<string> Options { get; set; } = [];

    public List<PollBallot> Ballots { get; set; } = [];

    public DateTimeOffset OpenedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>Active citizens when the poll closed; more than half of them had to vote.</summary>
    public int Electorate { get; set; }

    /// <summary>The chosen technology; null while open, or when the poll was cancelled.</summary>
    public string? Winner { get; set; }

    public bool IsOpen => ClosedAt is null;

    public int VotesFor(string technologyId) => Ballots.Count(b => b.TechnologyId == technologyId);

    public PollBallot? BallotOf(Guid citizenId) => Ballots.SingleOrDefault(b => b.CitizenId == citizenId);

    /// <summary>Votes that close the poll: more than half of the active citizens.</summary>
    public static int VotesNeeded(int activeCitizens) => activeCitizens / 2 + 1;
}

public sealed class PollBallot
{
    public Guid CitizenId { get; set; }

    public required string TechnologyId { get; set; }
}
