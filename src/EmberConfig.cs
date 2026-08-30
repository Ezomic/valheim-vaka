using BepInEx.Configuration;

namespace Ember
{
    /// <summary>
    /// Everything tunable, bound in one place so the .cfg reads as a document rather than as
    /// whatever order the code happened to need things in.
    ///
    /// The standing BepInEx trap applies: every entry is written to disk on first run and the
    /// saved value beats a new default in code. Changing a default here does nothing on a
    /// machine that has already run the plugin - edit
    /// <c>&lt;profile&gt;\BepInEx\config\ezomic.valheim.ember.cfg</c> as part of the same
    /// change. When a config-driven change appears to do nothing in game, read the cfg before
    /// reading any code.
    /// </summary>
    internal static class EmberConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> MaxFuelPerAbsence;

        public static ConfigEntry<bool> Verbose;

        public static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("Ember", "Enabled", true,
                "Whether the cap applies at all. Off leaves the plugin loaded and charging "
                + "vanilla's full bill for every absence, which is the behaviour this mod "
                + "exists to change - so off is only useful for telling the two apart without "
                + "uninstalling.");

            // Fuel units rather than seconds, and that choice is doing real work. Every kind of
            // fire has its own m_secPerFuel - a wall torch is 20000 seconds a resin, a wood
            // ground torch 10000 - so one number in seconds would be a whole torch for one and
            // a rounding error for another. Expressed in fuel it means the same thing
            // everywhere, including on fires added by other mods, which is the only version of
            // this setting a player can reason about without a table.
            MaxFuelPerAbsence = config.Bind("Ember", "MaxFuelPerAbsence", 1f,
                "How much fuel a fire may lose to a single absence, in the units the hover text "
                + "counts - logs, resin, whatever that fire burns. One, because a fire you "
                + "banked full should still be lit when you get back, and one unit off the top "
                + "is a bill you can pay by walking past it. It is not a discount on burning: "
                + "a fire you are standing next to consumes exactly what vanilla says, and this "
                + "number never touches it. 0 makes an absence free. Large numbers restore "
                + "vanilla, where a fire is charged for every hour that passed whether anyone "
                + "was there or not. A fire that was nearly empty when you left still goes out "
                + "- the cap limits what an absence costs, it does not conjure fuel.");

            // Not synced by intent - see the plugin. A diagnostic flag is personal, and a host
            // turning on someone else's logging is not a thing anybody asked for.
            Verbose = config.Bind("Diagnostics", "Verbose", false,
                "One line per fire whenever a bill is capped, naming the fire, the stretch of "
                + "time it was handed and what that would have cost. It is how you tell "
                + "\"the cap worked\" from \"nothing had burned down anyway\", which look "
                + "identical from in front of the fire. Quiet in normal play - a live fire "
                + "never reaches the cap - but a base with thirty torches writes thirty lines "
                + "the moment you walk into it.");
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
            // fire may never burn" rather than "this fire does not burn anyway".
            if (secondsPerFuel <= 0f) return -1.0;

            float fuel = MaxFuelPerAbsence.Value;
            if (fuel < 0f) fuel = 0f;

            return (double)secondsPerFuel * fuel;
        }
    }
}
