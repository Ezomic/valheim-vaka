using HarmonyLib;

namespace Ember
{
    /// <summary>
    /// One postfix, on one private method, changing one number.
    ///
    /// The whole mechanism is worth having in front of you, because it is not where anybody
    /// looks first. A fireplace does not burn down in the background. While its zone is
    /// loaded, <c>Fireplace.UpdateFireplace</c> runs every two seconds off an InvokeRepeating
    /// started in Awake, and each run asks <c>GetTimeSinceLastUpdate()</c> how long it has
    /// been - which diffs <c>ZNet.instance.GetTime()</c> against a <c>s_lastTime</c> stamp on
    /// the ZDO, re-stamps it to now, and returns the gap in seconds. UpdateFireplace then
    /// subtracts <c>gap / m_secPerFuel</c> fuel.
    ///
    /// When the zone is unloaded nothing runs and nothing re-stamps, so the gap keeps growing
    /// on the ZDO. The first update after the zone comes back therefore bills the entire
    /// absence in one go. Nobody being there is not part of the sum at any point.
    ///
    /// The clock behind it is the world's, not the wall's. <c>ZNet.UpdateNetTime</c> only
    /// advances <c>m_netTime</c> on a server while <c>GetNrOfPlayers() > 0</c>, and it is
    /// saved into the world file, so an empty server is frozen and a singleplayer world
    /// resumes exactly where it stopped. What actually drains your base is every hour somebody
    /// else was online somewhere else - or every hour you spent across the map, in
    /// singleplayer too.
    ///
    /// So the fix is not a longer-burning fire. It is refusing to charge for the part of the
    /// gap nobody was present for, which is a cap on a single bill and nothing else.
    ///
    /// One drain this deliberately does not cover. <c>Fire.Dot</c>, the spreading-fire
    /// component, calls <c>Fireplace.AddFuel(-m_fuelBurnAmount)</c> on its own one second
    /// invoke and reaches the ZDO through RPC_AddFuelAmount, so nothing here sees it. It needs
    /// no cover: that loop only runs while the object is loaded, which is the case this mod
    /// has nothing to say about. No player-built fire carries the component anyway - the torch
    /// prefabs ripped from this install are Piece, ZNetView, WearNTear, Fireplace and nothing
    /// else.
    /// </summary>
    internal static class EmberPatches
    {
        /// <summary>The private method this mod exists to adjust.</summary>
        private const string Target = "GetTimeSinceLastUpdate";

        /// <summary>
        /// Patched on its own, behind a check and a catch, because the target is a private
        /// method matched by a string. That is the kind of name a game update renames without
        /// anyone noticing, and the difference matters: an unguarded PatchAll throws inside
        /// Awake, which leaves a stack trace in the log where the reason should be. This way a
        /// rename costs the feature and says so in one line, and fires go back to burning
        /// exactly as vanilla burns them.
        ///
        /// The name is checked before patching rather than only caught afterwards, so the
        /// message can name what is missing instead of quoting Harmony at somebody. Harmony
        /// resolves a target with DeclaredMethod, which is DeclaredOnly, so the lookup could
        /// not drift onto one of the three other classes carrying a private method of the same
        /// name - but do not copy this patch onto them. Beehive and SapCollector return float
        /// where Fireplace and ResourceRoot return double, and Harmony checks the declared
        /// __result type against the real return type at patch time.
        ///
        /// Worth knowing about the blast radius, because it is wider than it looks: Harmony
        /// resolves every target in a class before applying any of them, so one unresolvable
        /// name costs the whole class rather than the one patch. That is an argument for
        /// keeping a name-matched target in a class of its own, which here is the entire mod.
        /// </summary>
        internal static void Apply(Harmony harmony)
        {
            try
            {
                if (AccessTools.Method(typeof(Fireplace), Target) == null)
                {
                    EmberPlugin.Log.LogError(
                        "Ember is doing nothing: Fireplace." + Target + " is not in this build "
                        + "of the game, so there is nothing to cap and fires burn the vanilla "
                        + "way. That method is private and matched by name, so a game update is "
                        + "the likely reason.");
                    return;
                }

                harmony.PatchAll(typeof(EmberPatches));
            }
            catch (System.Exception e)
            {
                // Error rather than warning, and it says the mod is inert rather than naming a
                // feature. There is only the one patch, so a failure here is the whole mod, and
                // the next line in the log is the ordinary "- ready." that every mod in the
                // suite writes - which means "loaded", not "working".
                EmberPlugin.Log.LogError(
                    "Ember is doing nothing: could not patch Fireplace." + Target
                    + ", so fires burn the vanilla way. " + e.Message);
            }
        }

        /// <summary>
        /// Vanilla's own update interval, from the <c>InvokeRepeating("UpdateFireplace", 0f,
        /// 2f)</c> in <c>Fireplace.Awake</c>.
        /// </summary>
        private const double LiveInterval = 2.0;

        /// <summary>
        /// Above this many seconds, an update is a catch-up rather than a live tick.
        ///
        /// The cap has to know the difference, and the honest test is the one vanilla already
        /// answers: a gap much longer than the update interval means updates were not running,
        /// which only happens when the zone was unloaded or the game was shut. Three intervals
        /// leaves room for a frame hitch or a loading stall to be counted normally.
        ///
        /// This only ever decides anything when the cap itself is smaller than six seconds -
        /// at MaxFuelPerAbsence 1 the cap is hours - and that is exactly the case it is here
        /// for. Without it, setting MaxFuelPerAbsence to 0 would clamp every two-second tick
        /// to zero as well, and "an absence is free" would quietly mean "fires never burn at
        /// all", which is a different mod and not the one the setting describes.
        /// </summary>
        private const double CatchUpAfter = LiveInterval * 3.0;

        /// <summary>
        /// Cap what one update may charge for.
        ///
        /// A postfix on the accessor rather than on UpdateFireplace, because this is the one
        /// place the number exists on its own. UpdateFireplace reads the fuel, decides whether
        /// the fire is burning, subtracts, clamps at zero, writes the ZDO and then repaints
        /// the whole visual state; a patch there would have to take all of that on to change
        /// the one term. Here there is a single double and no other consequence - and vanilla
        /// has already re-stamped s_lastTime to now before this runs, so the seconds trimmed
        /// off are discarded rather than deferred to the next update.
        ///
        /// It also means the mod cannot alter what an absence does to anything except fuel.
        /// GetTimeSinceLastUpdate has exactly one caller and this is its only use.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Fireplace), "GetTimeSinceLastUpdate")]
        private static void GetTimeSinceLastUpdate(Fireplace __instance, ref double __result)
        {
            // Unity overloads ==, so a destroyed component compares equal to null and the
            // null-propagating operators would walk straight past that. Plain == only.
            if (__instance == null) return;

            // A live tick is never touched, whatever the cap says. This is the line that keeps
            // the mod invisible while you are standing in front of the fire, and it is also
            // where all but a handful of calls leave: this runs every two seconds for every
            // fire the machine owns, and everything below is reached only on a catch-up.
            if (__result <= CatchUpAfter) return;

            double charged = __result;
            double cap = EmberConfig.CapSeconds(__instance.m_secPerFuel);
            bool capped = cap >= 0.0 && charged > cap;

            if (capped) __result = cap;

            if (EmberConfig.Verbose.Value) Report(__instance, charged, __result, capped);
        }

        /// <summary>
        /// Say what a catch-up was worth, whether or not the cap touched it.
        ///
        /// Reporting the uncapped ones as well is the point of this rather than a nicety.
        /// Fuel is drawn as <c>Mathf.Ceil(fuel)</c> out of the maximum, so anything under a
        /// whole unit is invisible in the hover text - which means "the cap worked" and "the
        /// patch never applied" look exactly the same from in front of the fire, and so do
        /// "the cap worked" and "nothing had burned down anyway". A line on every catch-up
        /// makes the log the instrument: walk out of the zone, wait, walk back, and either
        /// there is a line with the numbers in it or the mod is not running.
        /// </summary>
        private static void Report(Fireplace fire, double charged, double billed, bool capped)
        {
            // Named by prefab rather than by m_name: m_name is a localisation key like
            // $piece_sconce, which is not what somebody grepping a log will be looking for.
            string what = Utils.GetPrefabName(fire.gameObject.name);
            float rate = fire.m_secPerFuel;

            if (!capped)
            {
                EmberPlugin.Log.LogInfo(
                    what + " was away " + Span(charged) + " of world time, worth "
                    + Fuel(charged, rate) + " fuel. Under the cap, so it was charged in full.");
                return;
            }

            EmberPlugin.Log.LogInfo(
                what + " was away " + Span(charged) + " of world time, worth "
                + Fuel(charged, rate) + " fuel. Charged " + Span(billed) + ", "
                + Fuel(billed, rate) + " fuel.");
        }

        /// <summary>A span of seconds, in whichever unit reads without arithmetic.</summary>
        private static string Span(double seconds)
        {
            if (seconds < 90.0) return seconds.ToString("0") + "s";
            if (seconds < 5400.0) return (seconds / 60.0).ToString("0.0") + "m";

            return (seconds / 3600.0).ToString("0.0") + "h";
        }

        private static string Fuel(double seconds, float secondsPerFuel)
        {
            if (secondsPerFuel <= 0f) return "0";

            return (seconds / secondsPerFuel).ToString("0.00");
        }
    }
}
