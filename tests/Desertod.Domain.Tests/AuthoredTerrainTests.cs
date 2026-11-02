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

    [Theory]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public void The_camp_picture_is_not_walled_in_by_mountains(string id)
    {
        var wilds = Wilderness.Generate(Guid.Parse(id));
        var terrain = AuthoredTerrain.Generate(Wilderness.Seed(id), wilds);
        var seen = new bool[terrain.Length];
        var queue = new Queue<int>();
        var start = AuthoredTerrain.Index(15, 13);
        queue.Enqueue(start);
        seen[start] = true;
        var north = false;
        var south = false;
        var west = false;
        var east = false;
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            var x = node % AuthoredTerrain.Width + AuthoredTerrain.Left;
            var y = node / AuthoredTerrain.Width + AuthoredTerrain.Top;
            if (y == AuthoredTerrain.Top) north = true;
            if (y == AuthoredTerrain.Bottom - 1) south = true;
            if (x == AuthoredTerrain.Left) west = true;
            if (x == AuthoredTerrain.Right - 1) east = true;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var nx = x + dx;
                var ny = y + dy;
                if (!AuthoredTerrain.Inside(nx, ny)) continue;
                var next = AuthoredTerrain.Index(nx, ny);
                if (seen[next] || terrain[next] == AuthoredTerrain.Mountain) continue;
                seen[next] = true;
                queue.Enqueue(next);
            }
        }

        Assert.True(north && south && west && east);
    }
}
