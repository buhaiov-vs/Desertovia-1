namespace Desertod.Domain.Entities;

public enum Gender
{
    Male,
    Female,
}

/// <summary>
/// How a citizen looks. Every value is an index into a list the web client draws from the Kenney sprites,
/// so the counts below must match the client's lists.
/// </summary>
public sealed class Appearance
{
    public const int MaleStyles = 5;
    public const int FemaleStyles = 2;
    public const int SkinTones = 6;
    public const int HairColors = 8;
    public const int OutfitColors = 9;

    public Gender Gender { get; set; }
    public int Style { get; set; }
    public int SkinTone { get; set; }
    public int HairColor { get; set; }
    public int OutfitColor { get; set; }

    public static int StylesFor(Gender gender) => gender == Gender.Female ? FemaleStyles : MaleStyles;

    public bool IsValid() =>
        Enum.IsDefined(Gender)
        && Style >= 0 && Style < StylesFor(Gender)
        && SkinTone >= 0 && SkinTone < SkinTones
        && HairColor >= 0 && HairColor < HairColors
        && OutfitColor >= 0 && OutfitColor < OutfitColors;
}
