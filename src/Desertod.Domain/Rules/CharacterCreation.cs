using Desertod.Domain.Entities;
using Desertod.Domain.Stats;

namespace Desertod.Domain.Rules;

public static class CharacterCreation
{
    public const int NameMaximumLength = 32;

    public static Character CreateCharacter(string name, Appearance? appearance)
    {
        name = ValidName(name);
        if (appearance is null || !appearance.IsValid())
        {
            throw new GameRuleException("citizen.appearance", "This look is not available.");
        }

        return new Character { Name = name, Appearance = appearance };
    }

    /// <summary>The account's character moves into a village with nothing but its name and look: every stat is 0.</summary>
    public static Citizen Create(Guid villageId, Guid accountId, Character character, DateTimeOffset now) =>
        Create(villageId, accountId, character.Name, now, character.Appearance);

    /// <summary>A new citizen arrives with nothing but a name and a look: every stat is 0.</summary>
    public static Citizen Create(Guid villageId, Guid accountId, string name, DateTimeOffset now, Appearance? appearance = null)
    {
        name = ValidName(name);
        ValidateAppearance(appearance);

        var stats = new StatBlock();
        return new Citizen
        {
            VillageId = villageId,
            AccountId = accountId,
            Name = name,
            Appearance = appearance,
            Stats = stats,
            CreatedAt = now,
            StoredEnergy = Energy.Maximum(stats.Endurance),
            EnergyUpdatedAt = now,
            StoredCombatHealth = GameRules.CombatHealthBase + stats.Endurance * GameRules.CombatHealthPerEndurance,
            CombatHealthUpdatedAt = now,
        };
    }

    private static string ValidName(string name)
    {
        name = name.Trim();
        if (name.Length is 0 or > NameMaximumLength)
        {
            throw new GameRuleException(
                "citizen.nameLength",
                $"The name must be 1 to {NameMaximumLength} characters long.",
                new Dictionary<string, object> { ["maximum"] = NameMaximumLength });
        }

        return name;
    }

    private static void ValidateAppearance(Appearance? appearance)
    {
        if (appearance is not null && !appearance.IsValid())
        {
            throw new GameRuleException("citizen.appearance", "This look is not available.");
        }
    }
}
