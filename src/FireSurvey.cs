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
    /// lives in the prefabs, not in any code ilspycmd can show. The torch and brazier numbers
    /// were ripped from this install on 2026-08-30, and which fires burn coal never was. So the
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
    /// Run off the first bill of the first fire rather than from ZNetScene.Awake. Awake is
    /// before other mods have registered their prefabs - they do it from Update, retrying until
    /// ZNetScene exists - and before a client has been handed the host's settings. The first
    /// bill is after both, and it costs no second patch.
    ///
    /// Keyed on the scene and on the settings together. A new ZNetScene is a new world, and
    /// the settings changing under a loaded one - Core applying a host's values, or somebody
    /// editing the cfg live - would otherwise leave a log line describing a rule no longer in
    /// force. That second case re-surveys on the next fire to load, not instantly.
    /// </summary>
    internal static class FireSurvey
    {
        /// <summary>
        /// The scene last surveyed. Compared with Unity's ==, which is the point: the old
        /// world's ZNetScene is destroyed on logout, a destroyed object is == null, and so it
        /// never equals the next world's.
        /// </summary>
        private static ZNetScene _scene;

        private static string _signature;

        internal static void Check()
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null) return;

            string signature = VakaConfig.LightSignature();
            if (scene == _scene && signature == _signature) return;

            _scene = scene;
            _signature = signature;

            // Caught whole. This runs inside the fuel postfix, and a survey that threw there
            // would cost the bill it was called from - a diagnostic taking the feature down.
            try
            {
                Write(scene);
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

            /// <summary>Whether the rule changes anything about this fire.</summary>
            public bool Stretched
            {
                get { return !Infinite && Rate > 0f && Factor > 1.0; }
            }
        }

        private static void Write(ZNetScene scene)
        {
            List<Row> rows = new List<Row>();

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
                    Factor = VakaConfig.LightFactor(fuel)
                });
            }

            rows.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            VakaPlugin.Log.LogInfo(Summary(rows));

            if (VakaConfig.Verbose.Value) VakaPlugin.Log.LogInfo(Table(rows));
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
