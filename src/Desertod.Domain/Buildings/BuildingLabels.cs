using Desertod.Domain.Entities;

namespace Desertod.Domain.Buildings;

/// <summary>Display identity only. Global Number and every plot/history reference remain unchanged.</summary>
public sealed class BuildingDisplayNumber
{
    public int GlobalNumber { get; set; }
    public required string Family { get; set; }
    public int Value { get; set; }
}

public static class BuildingLabels
{
    // Upgraded versions share a sequence, so upgrading cannot collide with an existing label.
    public static string Family(string kind)=>kind switch
    {
        "WoodPile" or "Woodyard"=>"Woodyard",
        "StonePile" or "StoneYard"=>"StoneYard",
        "DryingRack" or "Granary"=>"Granary",
        "TownHall" or "Storehouse"=>"Storehouse",
        _=>kind
    };

    private static IEnumerable<(int Number,string Family)> Present(Village v)=>
        v.Structures.Select(s=>(s.Number,Family(s.Kind.ToString())))
            .Concat(v.Storages.Select(s=>(s.Number,Family(s.Kind.ToString()))))
            .Concat(v.Sites.Select(s=>(s.Number,Family(s.Kind.ToString()))))
            .Where(s=>s.Number>0).DistinctBy(s=>s.Number);

    public static void Ensure(Village v)
    {
        var existing=v.BuildingDisplayNumbers.Select(n=>n.GlobalNumber).ToHashSet();
        var last=v.BuildingDisplayNumbers.GroupBy(n=>n.Family).ToDictionary(g=>g.Key,g=>g.Max(n=>n.Value));
        foreach(var item in Present(v).OrderBy(n=>n.Number))
        {
            if(!existing.Add(item.Number))continue;
            var value=checked(last.GetValueOrDefault(item.Family)+1);last[item.Family]=value;
            v.BuildingDisplayNumbers.Add(new(){GlobalNumber=item.Number,Family=item.Family,Value=value});
        }
        // Retain demolished identities: later construction never reuses their labels.
    }

    public static IReadOnlyDictionary<int,int> Visible(Village v)
    {
        var present=Present(v).Select(n=>n.Number).ToHashSet();
        var rows=v.BuildingDisplayNumbers.Where(n=>present.Contains(n.GlobalNumber)).ToList();
        var counts=rows.GroupBy(n=>n.Family).ToDictionary(g=>g.Key,g=>g.Count());
        return rows.ToDictionary(n=>n.GlobalNumber,n=>counts[n.Family]>1?n.Value:0);
    }
}
