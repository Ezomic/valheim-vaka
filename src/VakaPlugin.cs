using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Ezomic.Core;
using HarmonyLib;

namespace Vaka
{
    /// <summary>
    /// Vaka. A fire may lose one fuel to an absence, however long the absence was.
    ///
    /// The complaint this comes from is a day away and a base of cold fires, with a bill in
    /// wood and resin to light them again - and nobody having been anywhere near them. The
    /// reason is not that fires burn too fast. It is what a fireplace is charged for.
    /// UpdateFireplace bills against the world clock, so the first update after your base
    /// loads back in pays for the entire gap since the last one, and whether a person was
    /// standing there is not part of the sum. A base of thirty torches is thirty of those
    /// bills landing in the same second you walk in.
    ///
    /// The obvious fix is to make fuel last longer, and it is the wrong one twice over. It
    /// makes a fire cheaper while you are stood at it cooking, which was never the complaint,
    /// and it only moves the deadline - a longer trip next time arrives at the same cold base.
    /// This caps the bill instead, so what an absence costs stops depending on how long it
    /// was, and what a fire costs while you are using it does not change at all.
    ///
    /// It is deliberately not an auto-feeder and not an infinite fire. One unit off the top is
    /// a real price, a fire left nearly empty still goes out, and every second you spend in
    /// front of a cooking fire is charged exactly what vanilla charges. Bank your fires before
    /// you log off and they will be lit when you get back; leave them guttering and they will
    /// not.
    ///
    /// The second rule, added on 2026-09-26 at Robbin's request, is the longer fuse argued
    /// against above - kept to lights, where that argument has nothing to bite on. A fire whose
    /// fuel item is on LightFuels (resin and coal by default) burns each unit
    /// LightFuelMultiplier times as long, present or absent. The case against a longer fuse was
    /// that it makes cooking cheaper while you stand at the fire; cooking fires burn wood, and
    /// nobody cooks over a torch. A standing brazier is less certain, and VakaPatches says why.
    /// It rides the same postfix as the cap - see VakaPatches for why that is the right seam
    /// rather than rewriting each fire's m_secPerFuel.
    ///
    /// Client-side is the wrong word for it, and the reason is worth writing down. Every
    /// decision here is made off state the machine already has, with nothing new on the wire -
    /// but the machine making it is whichever client owns the fireplace's ZDO, and the result
    /// is written into s_fuel, which is shared world state rather than something rendered
    /// locally. Ownership is not stable either: ZDOMan.ReleaseNearbyZDOS reassigns it every two
    /// seconds to whichever peer's active area covers the fire. So one person walking up to
    /// your base without this plugin takes the fire over and pays its absence off the vanilla
    /// way, and the cap is gone for everybody, silently.
    ///
    /// "Put it on the host and let the host decide" is not available as an alternative. A
    /// dedicated server never calls ZNet.SetReferencePosition - every caller is player code -
    /// so its reference position stays at the world origin and it is never a candidate owner
    /// for a player-built fire. There is no host simulation to put this in. Requirement.Everyone
    /// is therefore the honest answer rather than the cautious one: HostOnly's only power is to
    /// tolerate the absence of the plugin on the far end, and the far end is where all the
    /// arithmetic happens.
    ///
    /// There is deliberately no BepInProcess attribute. A dedicated server runs
    /// valheim_server.exe, and Core's gate only refuses on the server side of RPC_PeerInfo -
    /// so a mod that must be enforced has to be allowed to load there.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Soft, not hard. A hard dependency that is absent does not degrade - the plugin never
    // loads at all - and every mod here has to be installable on its own, because a stranger
    // should not need two installs to get one mod. Soft still buys the load-order guarantee
    // when Core is present, which is all that registering with the gate needs.
    [BepInDependency(CoreGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class VakaPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.vaka";
        public const string PluginName = "Vaka";
        public const string PluginVersion = "1.1.0";
        public const string PluginAuthor = "Robbin Thijssen";

        /// <summary>Core's plugin GUID. Optional - see TryRegisterWithCore.</summary>
        private const string CoreGuid = "ezomic.valheim.core";

        internal static ManualLogSource Log;

        /// <summary>
        /// Whether Core answered at load. Worth keeping even when nothing reads it yet: the
        /// difference between gated and ungated is invisible to a player otherwise, and this is
        /// what a warning on spawn would be driven by.
        /// </summary>
        internal static bool CorePresent;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            // Config first. Registering absorbs every entry the mod has bound, so anything bound
            // after this line is carried only because Core re-absorbs at manifest time - and
            // depending on the order of two lines in an Awake is not a thing worth relying on.
            VakaConfig.Bind(Config);

            TryRegisterWithCore();

            // PatchAll over a named type, never the whole assembly - a bare PatchAll() walks
            // every type in the DLL, so a half-written patch class goes live the moment it
            // compiles. It goes through VakaPatches.Apply rather than being called here
            // because the one method this mod patches is private and matched by name, and the
            // whole mod is that one patch: a rename in a game update should cost the feature
            // and one clear line in the log, not an exception thrown out of Awake.
            _harmony = new Harmony(PluginGuid);
            VakaPatches.Apply(_harmony);

            // The startup line every mod in the suite writes. It is how a log answers "which
            // build of what is actually loaded" without anyone guessing.
            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");
        }

        /// <summary>
        /// The light rule's log line, polled rather than hooked. There is no single moment
        /// that means "a world is loaded, other mods' fires are registered and the host's
        /// settings have arrived", so FireSurvey watches for the local player and for the
        /// rule's settings changing, and does nothing on every other frame. It used to ride the
        /// fuel postfix, which only runs on the machine that owns a fire - see FireSurvey for
        /// the player who never got the line because of that.
        /// </summary>
        private void Update()
        {
            FireSurvey.Tick();
        }

        /// <summary>
        /// Joins Core's version gate when Core is installed, and does nothing when it is not.
        ///
        /// Standing alone costs the enforcement, and here that is most of the point. Without
        /// Core nothing refuses a client that lacks this plugin, and one such client wandering
        /// through somebody's base is enough to charge the full absence for everyone - so the
        /// cap becomes an agreement between players rather than a property of the world, and
        /// the way it fails is a base of cold fires with every log looking reasonable. That is
        /// a real loss and it is the server owner's to accept, which is why this logs rather
        /// than refusing to run.
        /// </summary>
        private void TryRegisterWithCore()
        {
            CorePresent = Chainloader.PluginInfos.ContainsKey(CoreGuid);

            if (!CorePresent)
            {
                Log.LogInfo("Core not installed - running standalone, without the version gate.");
                return;
            }

            RegisterWithCore();
        }

        /// <summary>
        /// Kept separate and never inlined on purpose. The JIT resolves the assemblies a method
        /// needs when it first compiles that method, so a Suite call sitting directly in Awake
        /// would drag Ezomic.Core in before the check above could prevent it - and the
        /// missing-assembly exception would land during plugin load, which is the exact failure
        /// this arrangement exists to avoid. Isolating it means the type is only ever resolved
        /// on a machine that has Core.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RegisterWithCore()
        {
            // Everyone, not HostOnly, and it is not a preference. Skaft's HostOnly test has
            // three clauses - registers no prefab, invents no ZDO key, changes no item data -
            // and this mod passes the first two and fails the third: it writes ZDOVars.s_fuel
            // at a rate the far end does not agree with. HostOnly would let in exactly the
            // client that undoes the rule, and there is no host simulation to fall back on.
            Suite.Register(PluginGuid, PluginName, PluginVersion, Config, Requirement.Everyone);

            // Registering already absorbs the whole config file, so naming these is a
            // formality. It is worth writing anyway: this is the mod's entire balance plus its
            // kill switch, and saying out loud that the host owns both is the point of putting
            // Vaka on a server at all. A guest running a cap of 50 would be playing with
            // fires that never go out on somebody else's world.
            //
            // The light rule's two entries are balance in exactly the same sense, and for the
            // same ownership reason the cap is: a torch burns at whatever rate its current
            // owner's plugin says, and ownership moves every two seconds. A guest with a
            // multiplier of 10 would keep everyone's torches lit while standing near them, and
            // the torches would go back to burning at the host's rate the moment they left.
            Suite.Sync(VakaConfig.Enabled, VakaConfig.MaxFuelPerAbsence,
                       VakaConfig.LightFuels, VakaConfig.LightFuelMultiplier);

            // Opting the diagnostic back out. A host reaching across to switch on someone's
            // logging for the evening is not a thing anybody asked for, and a log line cannot
            // desync a world.
            Suite.Local(VakaConfig.Verbose);
        }

        private void OnDestroy()
        {
            // UnpatchSelf, never UnpatchAll(). The argumentless one unpatches every mod in the
            // process, not just this one.
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }
}
