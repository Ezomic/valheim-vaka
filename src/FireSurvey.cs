using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Vaka
{
    /// <summary>
    /// Which fires the light rule reaches, written to the log once per world.
    ///
    /// The rule is keyed on a fire's fuel item, and which fire burns what is asset data: it
    /// lives in the prefabs, not in any code ilspycmd can show. The fires' rates and capacities
    /// were ripped from this install on 2026-08-30, but a rip prints only the simple fields on
    /// a prefab's root and m_fuelItem is a reference, so their fuels never were. So the
    /// honest source for "what does LightFuels actually touch" is the running game, asked at
    /// the moment it matters, and this is that question written down. It is also the only way
    /// a player can see the rule is on: the hover text shows fuel, never a burn rate, and a
    /// torch that lasts twice as long looks exactly like a torch until the evening is over.
    ///
    /// Two levels, because they answer different people. One Info line always, naming the
    /// fires the rule applies to grouped by fuel - short enough to live in every log, and the
    /// first thing to read when somebody says a torch went out. And with Verbose on, every
    /// fireplace in the game with its fuel, its seconds per fuel and what the rule made of it,
    /// which is where a name for LightFuels gets copied from.
    ///
    /// Run from the plugin's Update, once this machine has a local player in a loaded world.
    /// That moment is after everything the list depends on. Other mods register their prefabs
    /// from Update, retrying until ZNetScene exists, and the player is only spawned once the
    /// area around it is ready, which is many frames after that. Core hands a client the host's
    /// settings from the server's RPC_PeerInfo, in the connection handshake, long before the
    /// character spawns - and if a host pushes again later, the settings check below catches
    /// that too.
    ///
    /// It used to run off the first bill of the first fire, and that was wrong in a way the
    /// README then repeated. A fire is billed only by the machine that owns it -
    /// UpdateFireplace calls GetTimeSinceLastUpdate inside <c>m_nview.IsOwner()</c> - and
    /// ZDOMan.ReleaseNearbyZDOS leaves a fire with its owner for as long as that player's
    /// active area covers it. So a player who walked into a base where somebody else was
    /// already standing never billed a fire, never got the line, and was told by the README
    /// that its absence meant the rule was off. A new world with nothing built yet had the
    /// same hole. The local player has no such gate: every player's game has one.
    ///
    /// A dedicated server has no local player, so it never writes the line. It never bills a
    /// player-built fire either (see VakaPlugin), so there is nothing there for the line to
    /// describe.
    ///
    /// Keyed on the scene and on the rule's settings together. A new ZNetScene is a new world,
    /// and the settings changing under a loaded one - Core applying a host's values, a config
    /// manager, or Devkit's reload button re-reading the file - would otherwise leave a log
    /// line describing a rule no longer in force. Both re-survey on the next frame. Verbose is
    /// watched separately: switching it on in a loaded world prints the table then and there,
    /// rather than promising one at the next login. A hand edit to the cfg file reaches none
    /// of this until something re-reads the file, because BepInEx 5 does not watch it.
    /// </summary>
    internal static class FireSurvey
    {
        /// <summary>
        /// The scene last surveyed. Compared with Unity's ==, which is the point: the old
        /// world's ZNetScene is destroyed on logout, a destroyed object is == null, and so it
        /// never equals the next world's.
        /// </summary>
        private static ZNetScene _scene;

        // The settings the last survey was written against, held as the values themselves
        // rather than as one string built from them. This is read every frame, and building a
        // signature string every frame would be an allocation per frame for the life of the
        // process, to answer a question whose answer is "no change" almost every time.
        private static bool _enabled;
        private static float _multiplier;
        private static string _fuels;

        /// <summary>Whether the Verbose table has been printed for the current survey.</summary>
        private static bool _tabled;

        /// <summary>The rows of the current survey, kept so Verbose can print them late.</summary>
        private static List<Row> _rows;

        /// <summary>
        /// Called from the plugin's Update. Cheap on the frames it does nothing, which is all
        /// but a handful per session: two static reads, four config reads and a string compare.
        /// </summary>
        internal static void Tick()
        {
            // Nothing to describe when the patch never went on. The load error already says so,
            // and a line claiming lights last twice as long after it would contradict it.
            if (!VakaPatches.Applied) return;

            ZNetScene scene = ZNetScene.instance;
            if (scene == null) return;

            // Plain ==, because Unity's overload is what treats a destroyed player as null. This
            // is also what keeps the survey off a dedicated server, which never has one.
            if (Player.m_localPlayer == null) return;

            bool enabled = VakaConfig.Enabled.Value;
            float multiplier = VakaConfig.LightFuelMultiplier.Value;
            string fuels = VakaConfig.LightFuels.Value ?? "";
            bool verbose = VakaConfig.Verbose.Value;

            // float.Equals rather than ==, and it matters here more than anywhere else in the
            // mod. NaN == NaN is false, so a cfg hand-edited to NaN would read as "changed" on
            // every frame and write the survey sixty times a second. Equals calls two NaNs
            // equal.
            bool same = scene == _scene && enabled == _enabled
                        && multiplier.Equals(_multiplier) && fuels == _fuels;

            if (same)
            {
                if (!verbose || _tabled) return;

                // Verbose switched on under a world already surveyed: the table alone, since
                // the one-line summary above it in the log is still true.
                _tabled = true;
                Guarded(() => VakaPlugin.Log.LogInfo(Table(_rows)));
                return;
            }

            // State first, write second. If Write throws, the next frame sees nothing changed
            // and does not try again, so a broken survey costs one warning rather than one per
            // frame.
            _scene = scene;
            _enabled = enabled;
            _multiplier = multiplier;
            _fuels = fuels;
            _tabled = verbose;
            _rows = new List<Row>();

            Guarded(() => Write(scene, verbose));
        }

        /// <summary>
        /// Caught whole. The survey is a diagnostic running inside the plugin's Update, and an
        /// exception out of it would repeat on every frame and bury the log it exists to help.
        /// </summary>
        private static void Guarded(Action write)
        {
            try
            {
                write();
            }
            catch (Exception e)
            {
                VakaPlugin.Log.LogWarning("Could not list this world's fireplaces: " + e.Message);
            }
        }

        private sealed class Row
        {
            public string Name;
            public string Fuel;
            public float Rate;
            public bool Infinite;
            public double Factor;

            /// <summary>
            /// Whether the prefab carries an EffectArea marked Burning anywhere under it, which
            /// is what a cooking station and a cauldron look for when they decide whether they
            /// stand over a fire. See the VakaPatches summary for why this is reported and not
            /// acted on.
            /// </summary>
            public bool Cooks;

            /// <summary>Whether the rule changes anything about this fire.</summary>
            public bool Stretched
            {
                get { return !Infinite && Rate > 0f && Factor > 1.0; }
            }
        }

        private static void Write(ZNetScene scene, bool verbose)
        {
            List<Row> rows = _rows;

            foreach (GameObject prefab in Prefabs(scene))
            {
                // Plain == and TryGetComponent, never ?. on a UnityEngine.Object.
                if (prefab == null) continue;

                Fireplace fire;
                if (!prefab.TryGetComponent(out fire)) continue;

                string fuel = VakaPatches.FuelOf(fire);

                rows.Add(new Row
                {
                    Name = prefab.name,
                    Fuel = fuel,
                    Rate = fire.m_secPerFuel,
                    Infinite = fire.m_infiniteFuel,
                    Factor = VakaConfig.LightFactor(fuel),
                    Cooks = HasBurningArea(prefab)
                });
            }

            rows.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            VakaPlugin.Log.LogInfo(Summary(rows));

            if (verbose) VakaPlugin.Log.LogInfo(Table(rows));
        }

        /// <summary>
        /// Whether anything under this prefab is an EffectArea with the Burning flag.
        ///
        /// Inactive children included, and they have to be: the warmth and burn areas of a fire
        /// sit under <c>_enabled_high</c> on the campfire and the brazier and under
        /// <c>_enabled</c> on the torches, which the Fireplace switches on only while it is
        /// lit, so a prefab read at rest may well have them off. m_type is a
        /// flags enum - FireBurn on a campfire is expected to be Burning alongside something
        /// else - so this tests the bit, not equality.
        /// </summary>
        private static bool HasBurningArea(GameObject prefab)
        {
            foreach (EffectArea area in prefab.GetComponentsInChildren<EffectArea>(true))
            {
                if (area == null) continue;
                if ((area.m_type & EffectArea.Type.Burning) != 0) return true;
            }

            return false;
        }

        /// <summary>
        /// The one line every log gets. Names only, grouped by the fuel that put them there, in
        /// the order LightFuels lists the fuels.
        ///
        /// A listed fuel that no fire burns says so rather than being left out. Coal is the case
        /// that made this necessary: it is in the default because Robbin asked for it, and nothing
        /// offline says which fire burns it, so "no fireplace in this world burns Coal" is an
        /// answer worth printing. It is also what a typo in the cfg looks like.
        /// </summary>
        private static string Summary(List<Row> rows)
        {
            string off = VakaConfig.LightRuleOffBecause();
            if (off != null)
                return "Light fuel rule is off (" + off + "). " + rows.Count
                       + " fireplaces in this world, every one at vanilla's rate.";

            IList<string> fuels = VakaConfig.LightFuelNames();
            StringBuilder text = new StringBuilder();

            text.Append("Light fuels last ")
                .Append(VakaConfig.LightFuelMultiplier.Value.ToString("0.##"))
                .Append("x as long.");

            foreach (string fuel in fuels)
            {
                List<string> names = new List<string>();

                foreach (Row row in rows)
                    if (row.Stretched && string.Equals(row.Fuel, fuel, StringComparison.OrdinalIgnoreCase))
                        names.Add(row.Name);

                text.Append(' ').Append(fuel).Append(": ");
                text.Append(names.Count == 0
                    ? "no fireplace in this world burns it."
                    : string.Join(", ", names.ToArray()) + ".");
            }

            // The one open question the rule leaves: a stretched fire a cooking station would
            // accept as its fire cooks on the stretched fuel. Named here rather than only in
            // the Verbose table, because it is a design question for whoever reads the log and
            // one run answers it.
            List<string> cooks = new List<string>();

            foreach (Row row in rows)
                if (row.Stretched && row.Cooks) cooks.Add(row.Name);

            if (cooks.Count > 0)
                text.Append(" A cooking station or cauldron placed over these counts them as a fire: ")
                    .Append(string.Join(", ", cooks.ToArray())).Append('.');

            return text.ToString();
        }

        /// <summary>
        /// Every fireplace, one per line, for Verbose.
        ///
        /// Each column is PadRight and then a space, never PadRight alone. PadRight is a
        /// minimum rather than a width, so a name longer than its column runs straight into
        /// the next one - Devkit's scenario summary printed a name glued to its result that
        /// way until a long enough name turned up.
        /// </summary>
        private static string Table(List<Row> rows)
        {
            StringBuilder text = new StringBuilder();
            text.Append("Every fireplace in this world, ").Append(rows.Count).Append(" of them:");

            foreach (Row row in rows)
            {
                text.Append('\n').Append("   ")
                    .Append(row.Name.PadRight(28)).Append(' ')
                    .Append((row.Fuel.Length == 0 ? "(no fuel)" : row.Fuel).PadRight(16)).Append(' ');

                if (row.Infinite)
                {
                    text.Append("infinite fuel, nothing to stretch");
                }
                else if (row.Rate <= 0f)
                {
                    text.Append("burns no fuel");
                }
                else if (row.Stretched)
                {
                    text.Append(row.Rate.ToString("0")).Append("s a fuel, burns as ")
                        .Append((row.Rate * row.Factor).ToString("0")).Append("s - light fuel");
                }
                else
                {
                    text.Append(row.Rate.ToString("0")).Append("s a fuel, untouched");
                }

                if (row.Cooks) text.Append(", can heat a cooking station");
            }

            return text.ToString();
        }

        /// <summary>ZNetScene.m_namedPrefabs, which is private and has no public equivalent.</summary>
        private static AccessTools.FieldRef<ZNetScene, Dictionary<int, GameObject>> _named;
        private static bool _triedNamed;

        /// <summary>
        /// Every prefab the scene will resolve by name.
        ///
        /// The dictionary rather than the public m_prefabs list, because the dictionary is what
        /// the game actually looks prefabs up in, and a mod that registers a fire has to reach
        /// it or its fire never loads. Most mods add to both; one that added only to the
        /// dictionary would be missing from the list and present in the world.
        ///
        /// Bound on first use inside a try/catch, never in a static initialiser: a
        /// FieldRefAccess with a wrong name throws at type-init, and a class that fails to
        /// initialise throws on every member after. Falls back to m_prefabs if the field is
        /// gone, which loses only the rare mod described above.
        /// </summary>
        private static ICollection<GameObject> Prefabs(ZNetScene scene)
        {
            if (!_triedNamed)
            {
                _triedNamed = true;

                try
                {
                    _named = AccessTools.FieldRefAccess<ZNetScene, Dictionary<int, GameObject>>("m_namedPrefabs");
                }
                catch (Exception)
                {
                    _named = null;
                }
            }

            if (_named != null)
            {
                try
                {
                    Dictionary<int, GameObject> named = _named(scene);
                    if (named != null) return named.Values;
                }
                catch (Exception)
                {
                    // Fall through to the list.
                }
            }

            return scene.m_prefabs;
        }
    }
}
