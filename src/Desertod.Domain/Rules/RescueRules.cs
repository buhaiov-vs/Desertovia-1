using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.World;
namespace Desertod.Domain.Rules;

public static class RescueRules
{
    public const string TaskId="rescue-wounded";
    public const double HospitalRemainingFactor = .25;
    public const int DeliveryRenown = 2;
    public static bool HasHospital(Village village, string plot) => village.Structures.Any(s => s.PlotId == plot && s.Kind == BuildingKind.Hospital && s.RuinedAt is null && s.Damage < 100);
    public static TilePoint Ward(Village village) => village.Structures.FirstOrDefault(s=>s.Kind==BuildingKind.Hospital && s.RuinedAt is null && s.Damage<100) is {} hospital
        ? WorldMap.Yard(hospital.PlotId) : WorldMap.Places[WorldMap.Camp];
    public static bool NeedsHelp(Citizen patient,DateTimeOffset now) => CombatRules.IsInjured(patient,now)&&patient.HospitalizedAt is null&&!HospitalRules.IsAdmitted(patient,now)&&patient.DungeonRunId is null;
    public static void Check(Citizen helper,Citizen patient,DateTimeOffset now)
    {
        if(helper.Id==patient.Id || helper.VillageId!=patient.VillageId)throw new GameRuleException("rescue.invalid","Choose another wounded citizen of your village.");
        if(!NeedsHelp(patient,now))throw new GameRuleException("rescue.notNeeded","This citizen no longer needs carrying.");
        if(patient.RescueByCitizenId is not null)throw new GameRuleException("rescue.claimed","Someone is already carrying this citizen.");
        if(helper.Load.Any(g=>g.Amount>0))throw new GameRuleException("rescue.hands","Unload your hands before carrying someone.");
    }
    public static RescueJourney Prepare(Citizen helper, Citizen patient, Village village, DateTimeOffset now)
    {
        Check(helper, patient, now);
        var from = helper.Trip?.PositionAt(now) ?? helper.Spot ?? helper.IdleSpot ?? WorldMap.Places[WorldMap.Camp];
        var pickup = patient.Spot ?? patient.IdleSpot ?? patient.Trip?.PositionAt(now) ?? WorldMap.Places[WorldMap.Camp];
        return PrepareRoute(village, from, pickup, patient.Id, false, patient.WoundedAt, now, helper.MedicalWalk(now));
    }

    public static RescueJourney PrepareRoute(Village village, TilePoint from, TilePoint pickup,
        Guid patientId, bool tsar, DateTimeOffset? woundedAt, DateTimeOffset now, double walkMultiplier = 1)
    {
        BankAccess.Require(village, from); BankAccess.Require(village, pickup);
        var hospital = village.Structures.Where(s => HasHospital(village, s.PlotId))
            .OrderBy(s => WorldMap.Yard(s.PlotId).DistanceTo(pickup)).FirstOrDefault()
            ?? throw new GameRuleException("rescue.noHospital", "Build a working hospital before carrying a patient.");
        var nav = new RaidNavigation(village, pedestrian: true);
        var start = nav.NearestFree(from); var nearPatient = nav.NearestFree(pickup);
        if (nearPatient.DistanceTo(pickup) > 2) throw new GameRuleException("rescue.noPath", "There is no safe route to this patient.");
        var route = new RescueJourney { PatientId = patientId, PatientIsTsar = tsar, WoundedAt = woundedAt, HospitalPlotId = hospital.PlotId };
        var at = now;
        void Append(TilePoint point, double speed)
        {
            if (route.Steps.LastOrDefault() is { } previous)
                at += TimeSpan.FromSeconds(new TilePoint(previous.X, previous.Y).DistanceTo(point) * Exploration.SecondsPerTile / speed);
            route.Steps.Add(new() { X = point.X, Y = point.Y, At = at });
        }
        void Leg(TilePoint origin, TilePoint destination, double speed)
        {
            var current = origin;
            for (var i = 0; current.DistanceTo(destination) > .01 && i < 2000; i++)
            {
                var next = nav.Next(current, destination, false)
                    ?? throw new GameRuleException("rescue.noPath", "There is no safe route for the rescue.");
                if (next.DistanceTo(current) < .001) throw new GameRuleException("rescue.noPath", "The rescue route is blocked.");
                Append(next, speed); current = next;
            }
            if (current.DistanceTo(destination) > .01) throw new GameRuleException("rescue.noPath", "The rescue route is too long.");
        }
        Append(start, 1); Leg(start, nearPatient, walkMultiplier);
        at += TimeSpan.FromSeconds(3); Append(nearPatient, 1); route.PickupAt = at;
        Leg(nearPatient, nav.NearestFree(WorldMap.Yard(hospital.PlotId)), .65 * walkMultiplier);
        route.ArrivesAt = at;
        return route;
    }

    public static bool Admit(Citizen patient,Village village,DateTimeOffset now, Citizen? helper = null)
    {
        // Старое действие без фактического маршрута больше не госпитализирует пациента.
        if (helper?.RescueJourney is not { CompletedAt: null } trip || trip.PatientIsTsar
            || trip.PatientId != patient.Id || trip.WoundedAt != patient.WoundedAt
            || patient.RescueByCitizenId != helper.Id
            || trip.ArrivesAt > now || !NeedsHelp(patient, now) || !HasHospital(village, trip.HospitalPlotId)) return false;
        var remaining = patient.InjuredUntil!.Value - now;
        patient.InjuredUntil = now + TimeSpan.FromTicks((long)(remaining.Ticks * HospitalRemainingFactor));
        patient.HospitalizedAt=now;patient.RescueByCitizenId=null;patient.IdleSpot=WorldMap.Yard(trip.HospitalPlotId);patient.Spot=patient.IdleSpot;patient.Trip=null;
        trip.CompletedAt = now; trip.Delivered = true;
        if (patient.RescueRewardEligible) helper.Renown += DeliveryRenown;
        patient.RescueRewardEligible = false;
        return true;
    }

    public static bool AdmitTsar(Citizen helper, Village village, DateTimeOffset now)
    {
        if (helper.RescueJourney is not { PatientIsTsar: true, CompletedAt: null } trip
            || trip.PatientId != village.OwnerAccountId || trip.WoundedAt != village.TsarWoundedAt
            || trip.ArrivesAt > now || !(village.TsarInjuredUntil > now) || village.TsarHospitalizedAt is not null
            || village.TsarRescueByCitizenId != helper.Id || !HasHospital(village, trip.HospitalPlotId)) return false;
        var remaining = village.TsarInjuredUntil!.Value - now;
        village.TsarInjuredUntil = now + TimeSpan.FromTicks((long)(remaining.Ticks * HospitalRemainingFactor));
        village.TsarHospitalizedAt = now; village.TsarRescueByCitizenId = null;
        village.TsarSpot = WorldMap.Yard(trip.HospitalPlotId); village.TsarTrip = null;
        trip.CompletedAt = now; trip.Delivered = true;
        if (village.TsarRescueRewardEligible) helper.Renown += DeliveryRenown;
        village.TsarRescueRewardEligible = false;
        return true;
    }

    public static void Cancel(Citizen helper, Citizen? patient, DateTimeOffset now, Village? village = null)
    {
        if (helper.RescueJourney is { CompletedAt: null } trip)
        {
            var position = trip.PositionAt(now);
            helper.IdleSpot = position; helper.Spot = position; helper.Trip = null;
            if (patient is not null && patient.RescueByCitizenId == helper.Id)
            {
                if (now >= trip.PickupAt) { patient.Spot = position; patient.IdleSpot = position; patient.Trip = null; }
                patient.RescueByCitizenId = null;
            }
            if (trip.PatientIsTsar && village?.TsarRescueByCitizenId == helper.Id)
            {
                if (now >= trip.PickupAt) village.TsarSpot = position;
                village.TsarRescueByCitizenId = null;
            }
            trip.CompletedAt = now;
        }
        if (patient?.RescueByCitizenId == helper.Id) patient.RescueByCitizenId = null;
        helper.RescuePatientId = null;
    }
}
