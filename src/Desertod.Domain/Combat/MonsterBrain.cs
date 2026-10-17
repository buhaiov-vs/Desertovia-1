using Desertod.Domain.World;
using Nez.AI.UtilityAI;

namespace Desertod.Domain.Combat;

public sealed record MonsterProfile(string Role, double PropertyWeight, double CrowdAversion, bool Breach);
public sealed record MonsterObjective(string Kind, string Id, TilePoint Position, double Value, int Occupied = 0);

/// <summary>Чистое решение: не наносит урон и не меняет инвентари. Исполнитель проверяет дальность отдельно.</summary>
public static class MonsterBrain
{
    public static MonsterProfile Profile(EnemyDefinition enemy, string variant, int identity = 0) =>
        variant == "boss" || enemy.Kind is EnemyKind.Bear or EnemyKind.BarrowKing
            ? new("destroyer", 1.6, .5, true)
            : (enemy.Coordinated && identity % 3 != 0 || identity % 4 == 1)
                ? new("flanker", 1.2, 3, false)
                : enemy.Steals.Count > 0 && identity % 2 == 0
                    ? new("looter", 1.5, 2, false)
                    : new("destroyer", 1.3, 1, enemy.Damage > 0);

    public static MonsterObjective? Choose(MonsterProfile profile, TilePoint from,
        string? previous, IEnumerable<MonsterObjective> objectives)
    {
        var reasoner = new HighestScoreReasoner<Choice>();
        reasoner.SetDefaultConsideration(new FixedScoreConsideration<Choice>(0));
        foreach (var objective in objectives)
        {
            var property = objective.Kind is "building" or "wall";
            var value = objective.Value * (property ? profile.PropertyWeight : 1);
            var score = value / (1 + from.DistanceTo(objective.Position) * .35)
                / (1 + objective.Occupied * .45);
            if (objective.Id == previous) score *= 1.25; // Инерция намерения без вечной привязки к недостижимой цели.
            reasoner.AddConsideration(new FixedScoreConsideration<Choice>((float)score)
                { Action = new SelectObjective(objective) });
        }
        var result = new Choice();
        reasoner.Select(result)?.Execute(result);
        return result.Objective;
    }

    private sealed class Choice { public MonsterObjective? Objective; }
    private sealed class SelectObjective(MonsterObjective objective) : IAction<Choice>
    {
        public void Execute(Choice context) => context.Objective = objective;
    }
}
