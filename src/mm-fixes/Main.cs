using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityModManagerNet;

namespace MMFixes
{
    /// <summary>
    /// Fixes for bugs in the base game. Each fix is self-contained and logs what it does.
    ///
    /// Fix 1 — "reserve" contract in a race seat (found 11/07/26):
    /// The game decides which car a driver uses from his contract status, not his seat. A driver
    /// whose contract says Reserve but who holds one of the two race seats (e.g. signed with a
    /// reserve promise, then put in the car) is treated as a stand-in for an injured driver:
    ///   • CarManager.GetCarForDriver returns the injured driver's car, or car 1, instead of his own.
    ///     Part Fitting then shows car 1's parts on both panels.
    ///   • CarOpinion (driver morale: "my car vs my team-mate's") compares the wrong cars.
    ///   • Driver.carID and Team.GetDriverIndex return -1 for him.
    /// 1.0.1 (same day): the real culprit was the stored per-driver car number (Driver.mCarID, set by
    /// driver swaps). The "Reserve" label only shows the *promised* status. Both are now ignored:
    /// The fix: a driver sitting in race seat N gets car N, whatever his contract status says.
    /// A genuine reserve (reserve slot, standing in for an injured driver) is untouched.
    /// </summary>
    public static class Main
    {
        public static bool Enabled;
        public static UnityModManager.ModEntry.ModLogger Logger;
        private static readonly HashSet<string> sLogged = new HashSet<string>();
        private static List<EmployeeSlot> sSlots = new List<EmployeeSlot>();

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;
            modEntry.OnToggle = (e, on) => { Enabled = on; return true; };
            new Harmony(modEntry.Info.Id).PatchAll(Assembly.GetExecutingAssembly());
            Enabled = true;
            try
            {
                UnityEngine.GameObject keys = new UnityEngine.GameObject("MMFixesScoutBrowseKeys");
                UnityEngine.Object.DontDestroyOnLoad(keys);
                keys.AddComponent<ScoutBrowseKeys>();
            }
            catch (Exception e)
            {
                Logger.Error("Scouting browse keys failed to start: " + e.Message);
            }
            Logger.Log("Motorsport Manager Fixes 1.3.1 loaded: car follows race seat; car opinions kept fresh; scouting list browsing and marketability column; pit crew Renew All.");
            return true;
        }

        internal static void LogOnce(string key, string message)
        {
            if (Logger != null && sLogged.Add(key))
            {
                Logger.Log(message);
            }
        }

        /// <summary>The race seat (0 or 1) this driver occupies, or -1. Ignores the contract status.</summary>
        internal static int RaceSeat(Driver d)
        {
            if (d == null || d.contract == null)
            {
                return -1;
            }
            Team team = d.contract.GetTeam();
            if (team == null || team.contractManager == null || team.championship == null
                || team.championship.series == Championship.Series.EnduranceSeries)
            {
                return -1;
            }
            // The ref overload fills our own list: the other overload returns the contract manager's
            // shared cache, which a caller further up the stack may be iterating.
            List<EmployeeSlot> slots = sSlots;
            slots.Clear();
            team.contractManager.GetAllEmployeeSlotsForJob(Contract.Job.Driver, ref slots);
            int seats = Math.Min(Team.mainDriverCount, CarManager.carCount);
            for (int i = 0; slots != null && i < seats && i < slots.Count; i++)
            {
                if (!slots[i].IsAvailable() && slots[i].personHired == d)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>True when the fix applies: the driver sits in race seat N (single-seater/GT
        /// series), so car N is his car. That is what the race itself uses (Team.GetDriversForCar
        /// reads the seats). The stored per-driver car number (Driver.mCarID, changed by swaps) and
        /// the contract status can both disagree with the seat; the seat wins.</summary>
        internal static bool Applies(Driver d, out int seat)
        {
            seat = -1;
            if (!Enabled || d == null || d.contract == null)
            {
                return false;
            }
            seat = RaceSeat(d);
            return seat >= 0;
        }

        internal static int StoredCarId(Driver d)
        {
            try
            {
                return Traverse.Create(d).Field("mCarID").GetValue<int>();
            }
            catch (Exception)
            {
                return -99;
            }
        }

        /// <summary>Logs once per driver when the game's own answer would have differed.</summary>
        internal static void NoteMismatch(Driver d, int seat, string what)
        {
            int stored = StoredCarId(d);
            bool reserve = d.IsReserveDriver();
            if (stored >= 0 && stored != seat || reserve)
            {
                LogOnce(what + "|" + d.name, string.Format("FIX (race seat): {0} sits in race seat {1} (car {2}), but the game had him {3}{4}; using car {2} for {5}.",
                    d.name, seat, seat + 1,
                    stored >= 0 ? "stored as car " + (stored + 1) : "with no stored car",
                    reserve ? " and on a reserve-status contract" : "",
                    what));
            }
        }

        internal static string Describe(Driver d)
        {
            try
            {
                Team team = d.contract.GetTeam();
                Driver out_ = team.contractManager.GetDriverSittingOut();
                return string.Format("{0} holds race seat {1} (car {2}) on a reserve-status contract{3}",
                    d.name, RaceSeat(d), RaceSeat(d) + 1,
                    out_ != null ? "; sitting out this event: " + out_.name : "; nobody is sitting out");
            }
            catch (Exception)
            {
                return d != null ? d.name : "?";
            }
        }
    }

    [HarmonyPatch(typeof(CarManager), "GetCarForDriver")]
    internal static class GetCarForDriverPatch
    {
        private static bool Prefix(CarManager __instance, Driver inDriver, ref Car __result)
        {
            try
            {
                int seat;
                if (!Main.Applies(inDriver, out seat) || inDriver.contract.GetTeam() != __instance.team)
                {
                    return true;
                }
                __result = __instance.GetCar(seat);
                Main.NoteMismatch(inDriver, seat, "parts screens and morale");
                return false;
            }
            catch (Exception e)
            {
                Main.LogOnce("err-car", "FIX (race seat) failed in GetCarForDriver: " + e.Message);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(Driver), "get_carID")]
    internal static class DriverCarIdPatch
    {
        private static bool Prefix(Driver __instance, ref int __result)
        {
            try
            {
                int seat;
                if (!Main.Applies(__instance, out seat))
                {
                    return true;
                }
                Main.NoteMismatch(__instance, seat, "his car number");
                __result = seat;
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(Team), "GetDriverIndex")]
    internal static class DriverIndexPatch
    {
        private static bool Prefix(Team __instance, Driver inDriver, ref int __result)
        {
            try
            {
                int seat;
                if (!Main.Applies(inDriver, out seat) || inDriver.contract.GetTeam() != __instance)
                {
                    return true;
                }
                __result = seat;
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}

namespace MMFixes
{
    /// <summary>
    /// Fix 2 (1.2.0): driver car opinions go stale.
    /// The game stores each driver's opinion ("my parts", "my car vs my team-mate's") and only
    /// recalculates it when a part is fitted to *his* car. The team-mate's opinion also depends on
    /// that car, but it isn't refreshed, so it can go on complaining about a car that has changed.
    /// Opinions worked out while fix 1's bug was active also stay wrong until something changes.
    ///   • Opening Part Fitting recalculates both race drivers' opinions.
    ///   • When one car's opinion refreshes after a part change, the team-mate's refreshes too.
    /// Recalculating only updates the opinion and its projected morale change; the game still
    /// applies morale at its usual moment (travel to the event).
    /// </summary>
    internal static class Opinions
    {
        internal static void RecalcPlayerDrivers(string why)
        {
            if (!Main.Enabled || Game.instance == null || Game.instance.player == null || Game.instance.player.team == null)
            {
                return;
            }
            Team team = Game.instance.player.team;
            if (team.championship == null || team.championship.series == Championship.Series.EnduranceSeries)
            {
                return;
            }
            List<string> done = new List<string>();
            for (int i = 0; i < CarManager.carCount; i++)
            {
                Driver d = team.GetDriverForCar(i);
                if (d != null && d.carOpinion != null)
                {
                    d.carOpinion.CalculateDriverOpinions(d);
                    done.Add(d.name);
                }
            }
            Main.LogOnce("op|" + why, "FIX (car opinions): recalculated " + string.Join(", ", done.ToArray()) + " (" + why + ").");
        }

        internal static void RecalcTeammate(Car car)
        {
            if (!Main.Enabled || car == null || car.carManager == null || car.carManager.team == null)
            {
                return;
            }
            Team team = car.carManager.team;
            if (team.championship == null || team.championship.series == Championship.Series.EnduranceSeries)
            {
                return;
            }
            for (int i = 0; i < CarManager.carCount; i++)
            {
                if (i == car.identifier)
                {
                    continue;
                }
                Driver d = team.GetDriverForCar(i);
                if (d != null && d.carOpinion != null)
                {
                    d.carOpinion.CalculateDriverOpinions(d);
                }
            }
        }
    }

    [HarmonyPatch(typeof(CarPartFittingScreen), "OnEnter")]
    internal static class FittingScreenOpinionPatch
    {
        private static void Prefix()
        {
            try
            {
                Opinions.RecalcPlayerDrivers("Part Fitting opened");
            }
            catch (Exception e)
            {
                Main.LogOnce("op-err", "FIX (car opinions) failed on Part Fitting: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(Car), "Update")]
    internal static class CarUpdateOpinionPatch
    {
        // Car.Update runs every frame for every car: a cached field accessor, not reflection.
        private static readonly AccessTools.FieldRef<Car, bool> sRefresh = AccessTools.FieldRefAccess<Car, bool>("mRefreshDriverOpinion");

        private static void Prefix(Car __instance, out bool __state)
        {
            __state = false;
            try
            {
                __state = sRefresh(__instance);
            }
            catch (Exception)
            {
            }
        }

        private static void Postfix(Car __instance, bool __state)
        {
            if (!__state)
            {
                return;
            }
            try
            {
                if (__instance.carManager != null && __instance.carManager.team != null && __instance.carManager.team.IsPlayersTeam())
                {
                    Opinions.RecalcTeammate(__instance);
                    Main.LogOnce("op-mate", "FIX (car opinions): a car's parts changed; the team-mate's opinion was refreshed too.");
                }
            }
            catch (Exception e)
            {
                Main.LogOnce("op-mate-err", "FIX (car opinions) team-mate refresh failed: " + e.Message);
            }
        }
    }
}
