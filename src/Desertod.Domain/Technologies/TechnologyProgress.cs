namespace Desertod.Domain.Technologies;

/// <summary>Research of one technology in a village. Progress is kept when a vote chooses another technology.</summary>
public sealed class TechnologyProgress
{
    public required string TechnologyId { get; set; }

    /// <summary>Research work done, in cycles of a researcher with every stat at 0.</summary>
    public int Done { get; set; }
    public int KnowledgeWork { get; set; }

    /// <summary>Set once the technology is discovered.</summary>
    public DateTimeOffset? ResearchedAt { get; set; }

    /// <summary>Different citizens who researched it; a technology needs a crew of different people.</summary>
    public List<ResearchHand> Hands { get; set; } = [];

    public bool HasHand(Guid citizenId) => Hands.Any(h => h.CitizenId == citizenId);

    public void AddHand(Guid citizenId)
    {
        if (!HasHand(citizenId))
        {
            Hands.Add(new ResearchHand { CitizenId = citizenId });
        }
    }
}

public sealed class ResearchHand
{
    public Guid CitizenId { get; set; }
}
