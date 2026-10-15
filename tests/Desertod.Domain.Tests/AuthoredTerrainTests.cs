using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Desertod.Domain.World;

namespace Desertod.Domain.Tests;

public sealed class AuthoredTerrainTests
{
    public sealed class TerrainCase
    {
        public uint Seed {get;set;}
        public Wilds? Wilds {get;set;}
        public string Hash {get;set;}="";
        public int Blocked {get;set;}
        public int Marsh {get;set;}
    }
    public static IEnumerable<object[]> Cases()
    {
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        var path=Path.Combine(AppContext.BaseDirectory,"authored-terrain.json");
        return JsonSerializer.Deserialize<TerrainCase[]>(File.ReadAllText(path),options)!.Select(c=>new object[]{c});
    }
    [Theory]
    [MemberData(nameof(Cases))]
    public void Collision_matches_the_unchanged_browser_generator_for_all_map_tiles(TerrainCase sample)
    {
        var terrain=AuthoredTerrain.Generate(sample.Seed,sample.Wilds);
        var mask=terrain.Select(k=>AuthoredTerrain.Blocked(k)?(byte)1:k==AuthoredTerrain.Marsh?(byte)2:(byte)0).ToArray();
        Assert.Equal(sample.Blocked,mask.Count(k=>k==1));Assert.Equal(sample.Marsh,mask.Count(k=>k==2));
        Assert.Equal(sample.Hash,Convert.ToHexStringLower(SHA256.HashData(mask)));
    }
}
