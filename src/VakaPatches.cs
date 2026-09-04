using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Vaka
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
    internal static class VakaPatches
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
        /// where Fireplace and ResourceRoot return double, and ResourceRoot's signature is
        /// identical to this one, so a mistyped declaring type would patch it silently.
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
                    VakaPlugin.Log.LogError(
                        "Vaka is doing nothing: Fireplace." + Target + " is not in this build "
                        + "of the game, so there is nothing to cap and fires burn the vanilla "
                        + "way. That method is private and matched by name, so a game update is "
                        + "the likely reason.");
                    return;
                }

                harmony.PatchAll(typeof(VakaPatches));
            }
            catch (System.Exception e)
            {
                // Error rather than warning, and it says the mod is inert rather than naming a
                // feature. There is only the one patch, so a failure here is the whole mod, and
                // the next line in the log is the ordinary "- ready." that every mod in the
                // suite writes - which means "loaded", not "working".
                VakaPlugin.Log.LogError(
                    "Vaka is doing nothing: could not patch Fireplace." + Target
                    + ", so fires burn the vanilla way. " + e.Message);
            }
        }

        /// <summary>
        /// Fireplaces this machine has already billed at least once.
        ///
        /// This is the whole definition of "an absence", and getting it from the component's
        /// own history rather than from the size of the gap is the only version that holds up.
        /// A fireplace is created by ZNetScene when its zone loads and destroyed when the zone
        /// goes, so the first time we bill an instance is exactly the update that pays for
        /// however long the thing was not running. Every later call is a live two-second tick.
        ///
        /// The obvious alternative - treat any gap longer than a few seconds as an absence -
        /// is wrong in a way that is worth writing down, because it reads as correct. Sleeping
        /// through the night runs EnvMan.UpdateTimeSkip, which pushes the world clock forward
        /// at around fifty times normal for a dozen real seconds, and the console's skiptime
        /// jumps it instantly. Both of those arrive here as a huge gap with the zone loaded and
        /// the fire ticking the entire time. You were standing there. A size test forgives
        /// them; asking whether the component has been running does not.
        ///
        /// A ConditionalWeakTable rather than a set of instance ids: it holds the key weakly,
        /// so an entry disappears when the fireplace is collected, and Unity recycles instance
        /// ids. There is nothing to clear and nothing to leak.
        /// </summary>
        private static readonly ConditionalWeakTable<Fireplace, object> _billed =
            new ConditionalWeakTable<Fireplace, object>();

        /// <summary>
        /// Vanilla's own update interval, from the <c>InvokeRepeating("UpdateFireplace", 0f,
        /// 2f)</c> in <c>Fireplace.Awake</c>.
        /// </summary>
        private const double LiveInterval = 2.0;

        /// <summary>
        /// A first bill smaller than this is not worth calling an absence.
        ///
        /// Unlike the gap test this replaced, nothing about the cap depends on this number - a
        /// first bill is recognised by the instance never having been billed, not by its size.
        /// This only keeps a fire you just placed, or one whose zone blinked, out of the
        /// Verbose log, where a line reading "away 0s" is noise.
        /// </summary>
        private const double WorthReporting = LiveInterval * 3.0;

        /// <summary>
        /// Cap what the first update after a fireplace comes back may charge for.
        ///
        /// A postfix on the accessor rather than on UpdateFireplace, because this is the one
        /// place the number exists on its own. UpdateFireplace reads the fuel, decides whether
        /// the fire is burning, subtracts, clamps at zero, writes the ZDO and then repaints
        /// the whole visual state; a patch there would have to take all of that on to change
        /// the one term. Here there is a single double and no other consequence - and vanilla
        /// has already re-stamped s_lastTime to now before this runs, so the seconds trimmed
        /// off are discarded rather than deferred to the next update. That is what a cap wants,
        /// but it is a property of where the patch sits rather than a choice: a postfix here
        /// could not bank the remainder even if that were wanted.
        ///
        /// The clamp can only ever reduce, and every call advances the stamp, so successive
        /// bills partition real elapsed time rather than repeating it. Ownership moving between
        /// clients cannot make a fire burn faster than vanilla.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Fireplace), "GetTimeSinceLastUpdate")]
        private static void GetTimeSinceLastUpdate(Fireplace __instance, ref double __result)
        {
            // Unity overloads ==, so a destroyed component compares equal to null and the
            // null-propagating operators would walk straight past that. Plain == only.
            if (__instance == null) return;

            // Seen before means the component has been running, so this is a live tick and is
            // never touched however large it is. This is also where all but one call per
            // fireplace per zone load leaves, which matters: the postfix runs every two seconds
            // for every fire this machine owns.
            object marker;
            if (_billed.TryGetValue(__instance, out marker)) return;

            _billed.Add(__instance, null);

            double charged = __result;
            double cap = VakaConfig.CapSeconds(__instance.m_secPerFuel);

            // cap is negative when the mod is off, and NaN fails every comparison it is in, so
            // a config file hand-edited to NaN or a nonsense rate leaves vanilla alone rather
            // than writing a NaN into the fuel the world saves.
            if (cap >= 0.0 && charged > cap) __result = cap;

            if (VakaConfig.Verbose.Value) Report(__instance, charged, __result);
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
        ///
        /// Silent about fires that were never going to spend anything, and that is not
        /// tidiness. GetTimeSinceLastUpdate is called above vanilla's
        /// <c>IsBurning() &amp;&amp; !m_infiniteFuel &amp;&amp; state == 1</c> gate, so an
        /// unlit torch, a fire someone switched off, one standing under a roof and an
        /// infinite-fuel brazier all reach this with a large gap and nothing at stake.
        /// Reporting those would claim a saving that never existed, which is the same class of
        /// lie the Verbose flag exists to prevent. IsBurning covers blocked, switched off,
        /// underwater and out of fuel in one call, so the pair below is exactly vanilla's gate.
        /// </summary>
        private static void Report(Fireplace fire, double charged, double billed)
        {
            if (charged < WorthReporting) return;
            if (!fire.IsBurning() || fire.m_infiniteFuel) return;

            // Named by prefab rather than by m_name: m_name is a localisation key like
            // $piece_sconce, which is not what somebody grepping a log will be looking for.
            string what = Utils.GetPrefabName(fire.gameObject.name);
            float rate = fire.m_secPerFuel;

            if (billed >= charged)
            {
                VakaPlugin.Log.LogInfo(
                    what + " was away " + Span(charged) + " of world time, worth "
                    + Fuel(charged, rate) + " fuel. Under the cap, so it was charged in full.");
                return;
            }

            VakaPlugin.Log.LogInfo(
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
