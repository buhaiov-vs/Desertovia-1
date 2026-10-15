using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Weather;

namespace Desertod.Domain.Rules;

/// <summary>The same effective price drives cycle admission and the displayed action quote.</summary>
public static class WorkEnergy
{
    public static double TaskCost(Village village, TaskDefinition task, DateTimeOffset now) =>
        WeatherRules.EnergyCost(village, !WeatherRules.IsIndoor(task), now) *
        (task.Effect is TaskEffect.Build or TaskEffect.Repair ? 2 : task.Effect == TaskEffect.Supervise || task.Inputs.Count > 0 ? 1.5 : 1.25);

    public static double JobCost(Village village, JobDefinition job, DateTimeOffset now) =>
        WeatherRules.EnergyCost(village, true, now) * (job.Kind == JobKind.Labor ? 2 : 1.25);

    public static double WithMedical(Citizen citizen, double price, DateTimeOffset now) =>
        price * (citizen.HasMedicalEffect("snorers", now) ? 1.2 : 1);
}
