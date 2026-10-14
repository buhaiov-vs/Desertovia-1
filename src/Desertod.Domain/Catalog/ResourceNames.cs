namespace Desertod.Domain.Catalog;

/// <summary>English resource names for server messages; clients translate by <see cref="ResourceKind"/>.</summary>
public static class ResourceNames
{
    public static string Of(ResourceKind kind) => kind switch
    {
        ResourceKind.IronOre => "iron ore",
        ResourceKind.IronIngot => "iron",
        ResourceKind.ResearchPoints => "knowledge",
        ResourceKind.LeatherArmor => "leather armor",
        ResourceKind.VigorDrink => "vigor drink",
        _ => kind.ToString().ToLowerInvariant(),
    };
}
