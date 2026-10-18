using Desertod.Domain.Entities;

namespace Desertod.Api.Game;

/// <summary>Ready development logins for the built village. Absent outside Development.</summary>
public static class DevelopmentRoles
{
    public const string Tsar = "tsar";
    public const string Governor = "governor";
    public const string Villager = "villager";

    public const string TsarName = "Демо-зодчий";
    public const string GovernorName = "Демо-посадник";
    public const string VillagerName = "Демо-житель";

    /// <summary>Extra residents so a dungeon gathering can be filled from development logins.</summary>
    public static readonly IReadOnlyList<DevelopmentPlayer> Players =
    [
        new("spearman", "Демо-копейщик", Gender.Male, 0, 1, 2, 4),
        new("swordsman", "Демо-мечник", Gender.Male, 3, 3, 4, 1),
        new("archer", "Демо-лучник", Gender.Female, 1, 4, 6, 5),
        new("shieldbearer", "Демо-щит", Gender.Male, 4, 0, 0, 7),
        new("healer", "Демо-лекарь", Gender.Female, 0, 5, 3, 2),
    ];

    public static readonly IReadOnlyList<DevelopmentRoleChoice> Choices =
    [
        new(Tsar, TsarName),
        new(Governor, GovernorName),
        new(Villager, VillagerName),
        ..Players.Select(player => new DevelopmentRoleChoice(player.Role, player.Name)),
    ];
}

/// <param name="Style">Sprite style. Male styles are 0–4 and female styles are 0–1.</param>
public sealed record DevelopmentPlayer(string Role, string Name, Gender Gender, int Style, int SkinTone, int HairColor, int OutfitColor);

public sealed record DevelopmentRoleChoice(string Role, string Name);
