using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace Vaka
{
    /// <summary>
    /// Everything tunable, bound in one place so the .cfg reads as a document rather than as
    /// whatever order the code happened to need things in.
    ///
    /// The standing BepInEx trap applies: every entry is written to disk on first run and the
    /// saved value beats a new default in code. Changing a default here does nothing on a
    /// machine that has already run the plugin - edit
    /// <c>&lt;profile&gt;\BepInEx\config\ezomic.valheim.vaka.cfg</c> as part of the same
    /// change. When a config-driven change appears to do nothing in game, read the cfg before
    /// reading any code.
    /// </summary>
    internal static class VakaConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> MaxFuelPerAbsence;
        public static ConfigEntry<string> LightFuels;
        public static ConfigEntry<float> LightFuelMultiplier;

        public static ConfigEntry<bool> Verbose;

        public static void Bind(ConfigFile config)
        {
            // The kill switch for both rules, not only the cap it was written for. A setting
            // called Enabled that left half the mod running when it was off would be read as
            // broken by anybody who did not also read its description, and the README's
            // conclusive test uses it as the vanilla arm: with it off the torch has to burn
            // the way the game burns it, which it would not if the light rule survived.
            //
            // Same key and same default as 1.0, so no cfg needs editing. Only the text around
            // it changed, and BepInEx rewrites descriptions from code the next time it saves.
            Enabled = config.Bind("Vaka", "Enabled", true,
                "Whether Vaka does anything at all. Off leaves the plugin loaded with both of "
                + "its rules switched off: every absence is charged vanilla's full bill and "
                + "every fuel burns at vanilla's rate, which is the behaviour this mod exists "
                + "to change - so off is only useful for telling the two apart without "
                + "uninstalling.");

            // Fuel units rather than seconds, and that choice is doing real work. Every kind of
            // fire has its own m_secPerFuel - ripped from this install, the cooking fires
            // (campfire, iron campfire, hearth, bonfire) are 5000 seconds a wood, the wood
            // ground torch 10000, and the torches and standing braziers 20000 - so one number
            // in seconds would be four hours of a hearth and a fifth of a torch. Expressed in
            // fuel it means the same thing everywhere, including on fires added by other mods.
            //
            // It is also the unit the player acts in, which is the argument against expressing
            // it as a fraction of the tank instead: RPC_AddFuel adds exactly 1f per press
            // whatever the fire is, so a cap of one is one press of E to put right on all of
            // them. That holds even though one fuel is a twentieth of a hearth's twenty and a
            // quarter of a wood ground torch's four.
            MaxFuelPerAbsence = config.Bind("Vaka", "MaxFuelPerAbsence", 1f,
                "How much fuel a fire may lose to a single absence, in the units the hover text "
                + "counts - logs, resin, whatever that fire burns. One, because a fire you "
                + "banked full should still be lit when you get back, and one unit off the top "
                + "is a bill you can pay by walking past it. It is not a discount on burning: "
                + "a fire you are standing next to consumes exactly what vanilla says, and this "
                + "number never touches it. 0 makes an absence free. Large numbers restore "
                + "vanilla, where a fire is charged for every hour that passed whether anyone "
                + "was there or not. A fire that was nearly empty when you left still goes out "
                + "- the cap limits what an absence costs, it does not conjure fuel.");

            // Keyed on the fuel item rather than on a list of pieces, and that is doing the
            // work of the whole rule. The request was "resin and coal last twice as long", and
            // the thing that separates a light from a cooking fire in this game is what it
            // burns: every cooking fire ripped from this install burns wood, and the torches
            // and braziers burn resin. Reading m_fuelItem off each fire also means a torch
            // another mod adds is covered the moment it burns resin, with nothing to update
            // here - the same reason MaxFuelPerAbsence is counted in fuel.
            //
            // Prefab names, because that is what m_fuelItem points at and what a person can
            // copy out of the log: the Verbose survey prints every fireplace's fuel by exactly
            // this name. Matched without regard to case, since "resin" in a config file is
            // plainly meant to be Resin and no two vanilla items differ only by case.
            //
            // Which fires burn Coal is asset data and was never ripped. The survey logged on
            // every world load answers it on the machine in front of you, so the default names
            // Coal without claiming anything about which pieces that reaches.
            LightFuels = config.Bind("Vaka", "LightFuels", "Resin, Coal",
                "Fuel items whose fires burn longer, by prefab name, separated by commas. A "
                + "fire counts when the item it burns is on this list, so the rule follows the "
                + "fuel rather than the piece and covers torches added by other mods too. The "
                + "defaults are the two fuels lights burn; cooking fires burn wood and are left "
                + "alone, because a longer fuse on a campfire makes cooking cheaper and nobody "
                + "asked for that. Every world load writes one line to the log naming the fires "
                + "this reaches, and Verbose lists every fireplace with the name of its fuel, "
                + "ready to copy here. Empty switches the rule off.");

            // A multiplier on the fire's own rate, so every fire keeps its own proportions -
            // a wall torch still outlasts a brazier by the same margin, just further out.
            // One number in seconds would have given every light the same fuse, which is the
            // mistake MaxFuelPerAbsence's comment walks through.
            //
            // Floored at 1 in LightFactor rather than with an AcceptableValueRange. Below 1 is
            // "burn faster than vanilla", which is not a thing this mod does anywhere - the
            // postfix it runs in only ever reduces a bill, and that is what makes ownership
            // bouncing between clients safe. Handled in code rather than by the range so a
            // hand-edited NaN meets the same refusal CapSeconds gives it.
            LightFuelMultiplier = config.Bind("Vaka", "LightFuelMultiplier", 2f,
                "How many times longer each unit of a light fuel burns. 2 makes a torch that "
                + "held a resin for five and a half hours of world time hold it for eleven. It "
                + "applies whether you are at the fire or away from it, and it stacks with "
                + "MaxFuelPerAbsence in the obvious way: an absence still costs at most that "
                + "much fuel, it just takes longer to get there. 1 switches the rule off. "
                + "Anything below 1 is treated as 1, because Vaka never makes a fire burn "
                + "faster than vanilla.");

            // Not synced by intent - see the plugin. A diagnostic flag is personal, and a host
            // turning on someone else's logging is not a thing anybody asked for.
            Verbose = config.Bind("Diagnostics", "Verbose", false,
                "One line per fire every time one comes back from being unloaded, naming the "
                + "fire, how long it was away, what that was worth in fuel and what it was "
                + "actually charged - including the times the cap did not apply. This is the "
                + "only precise readout there is. Hover text draws fuel rounded up to a whole "
                + "unit, so a fire that lost a third of a log looks identical to one that lost "
                + "nothing, and both look identical to a mod that never loaded. Quiet while "
                + "you are near a fire, because a live update is two seconds and is never "
                + "reported, but a base with thirty torches writes thirty lines the moment you "
                + "walk into it. Also lists, once per world, every fireplace in the game with "
                + "the fuel it burns, its seconds per fuel and whether the light rule applies "
                + "to it.");
        }

        /// <summary>
        /// The most seconds of burning a single update may charge this fire for, or a negative
        /// number when the cap does not apply.
        ///
        /// Reads the config entry every call rather than caching. Core swaps a host's value
        /// straight into the entry on connect and puts it back on disconnect, and the config
        /// manager rewrites it live, so a value read once at load is a value that goes stale
        /// the first time somebody joins a server.
        /// </summary>
        internal static double CapSeconds(float secondsPerFuel)
        {
            if (!Enabled.Value) return -1.0;

            // Vanilla's own guard in UpdateFireplace, restated because this is reached from a
            // method that does not carry it. A fire with no burn rate is one that never spends
            // fuel, and multiplying by it would give a cap of zero - which would read as "this
            // fire may never burn" rather than "this fire does not burn anyway". The rate is a
            // public field another mod or a future prefab can set to anything, so it is checked
            // here rather than trusted from the caller.
            if (secondsPerFuel <= 0f || float.IsNaN(secondsPerFuel)
                || float.IsInfinity(secondsPerFuel)) return -1.0;

            float fuel = MaxFuelPerAbsence.Value;

            // NaN reaches this from a hand-edited cfg: BepInEx parses a float with
            // NumberFormatInfo.InvariantInfo, whose NaNSymbol is the literal "NaN". Left alone
            // it would multiply through to a NaN cap, and vanilla's subtraction floors fuel at
            // zero without ever testing for NaN - so the fire's stored fuel would become NaN,
            // be saved into the world, fail every "> 0" test forever, and survive RPC_AddFuel
            // because Mathf.Clamp leaves NaN alone. One bad character in a config file would
            // permanently kill a fire. Refusing the cap here leaves vanilla in charge instead.
            if (float.IsNaN(fuel)) return -1.0;
            if (fuel < 0f) fuel = 0f;

            return (double)secondsPerFuel * fuel;
        }

        /// <summary>
        /// How many times longer a unit of this fuel lasts, or exactly 1 when the light rule
        /// does not apply to it.
        ///
        /// Read from the entries on every call, for the reason CapSeconds gives: Core swaps a
        /// host's values in on connect and back out on disconnect, so anything remembered from
        /// load is stale the first time somebody joins a server. The list is the one part that
        /// costs anything to read, and it is re-parsed only when its text changes.
        ///
        /// Never below 1, and 1 for anything that is not a finite number. NaN is the same trap
        /// CapSeconds documents - it would divide through to a NaN bill, and a NaN written into
        /// s_fuel kills a fire for good - and infinity would divide every bill down to zero,
        /// which is "never burns" arrived at by accident rather than asked for.
        /// </summary>
        internal static double LightFactor(string fuelPrefab)
        {
            if (!Enabled.Value) return 1.0;
            if (string.IsNullOrEmpty(fuelPrefab)) return 1.0;

            float factor = LightFuelMultiplier.Value;
            if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 1f) return 1.0;

            return Parsed().Contains(fuelPrefab) ? factor : 1.0;
        }

        /// <summary>
        /// Why the light rule is doing nothing at all, in the words of the cfg, or null when it
        /// is on. The same three tests LightFactor makes, kept beside it so the log's reason and
        /// the rule's behaviour cannot drift apart.
        /// </summary>
        internal static string LightRuleOffBecause()
        {
            if (!Enabled.Value) return "Enabled is false, so Vaka is off entirely";

            float factor = LightFuelMultiplier.Value;
            if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 1f)
                return "LightFuelMultiplier is " + factor + ", and only a number above 1 stretches anything";

            if (LightFuelNames().Count == 0) return "LightFuels is empty";

            return null;
        }

        /// <summary>
        /// The configured fuel names, in the order they were written, for the survey to report
        /// against. Order kept so the log line reads back in the same order as the cfg.
        /// </summary>
        internal static IList<string> LightFuelNames()
        {
            Parsed();
            return _parsedOrder;
        }

        /// <summary>
        /// Everything the light rule depends on, as one string. The survey keys on it so that a
        /// host's values arriving, or a live edit, re-writes the list of affected fires rather
        /// than leaving a log line that describes settings no longer in force.
        /// </summary>
        internal static string LightSignature()
        {
            return Enabled.Value + "|"
                   + LightFuelMultiplier.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                   + "|" + (LightFuels.Value ?? "");
        }

        private static string _parsedFrom;

        private static readonly HashSet<string> _parsed =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<string> _parsedOrder = new List<string>();

        /// <summary>
        /// The list, split once per distinct value of the entry. Commas and semicolons both
        /// separate, since a hand-edited list in a config file gets whichever the person's
        /// fingers reach for, and no prefab name contains either.
        /// </summary>
        private static HashSet<string> Parsed()
        {
            string raw = LightFuels.Value ?? "";
            if (_parsedFrom != null && raw == _parsedFrom) return _parsed;

            _parsed.Clear();
            _parsedOrder.Clear();

            foreach (string part in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = part.Trim();
                if (name.Length == 0) continue;

                if (_parsed.Add(name)) _parsedOrder.Add(name);
            }

            _parsedFrom = raw;
            return _parsed;
        }
    }
}
