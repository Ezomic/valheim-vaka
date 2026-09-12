# Vaka

Vaka caps what a fireplace loses while nobody is there. A single absence costs at most one
fuel, however long it lasted. Burning while you are standing at the fire is untouched.

## Features

- A fire is charged at most one fuel for any one absence. The amount is configurable.
- Live burning is exactly vanilla, to the decimal. Sleeping and `skiptime` are still charged
  in full, because the fire was loaded and ticking the whole time.
- Covers every piece that uses the vanilla `Fireplace` component, including fires added by
  other mods.
- No new pieces, no keybinds, no UI, no new saved data. One Harmony postfix on one method.
- Optional logging that reports what each catch-up was worth and what it was actually charged.

## How it works

A fireplace does not burn in the background. While its zone is loaded,
`Fireplace.UpdateFireplace` runs every two seconds, asks how long it has been since the last
run, and subtracts that many seconds of fuel. When the zone is unloaded nothing runs and
nothing re-stamps the timestamp on the fire's network object, so the gap keeps growing. The
first update after your base loads back in pays for the whole gap at once. Whether anyone was
present is never part of the sum, so a base of thirty torches is thirty of those bills landing
in the second you walk in.

The clock is world time, not wall time. A dedicated server only advances it while at least one
player is connected, and the value is saved into the world file. An empty server is frozen and
a single player world resumes exactly where it stopped. What actually drains your base is the
hours other people spent online elsewhere on the map, or in single player, the hours you spent
across it yourself.

Vaka caps that first bill rather than making fuel last longer. A longer fuse would make a fire
cheaper while you are cooking at it, which was never the problem, and a longer trip next time
would still arrive at a cold base.

An absence is recognised by the fireplace object not having been running, not by the size of
the gap. The game creates a fireplace when its zone loads and destroys it when the zone goes,
so the first update for a new object is the one paying off however long it was away. Every
later update is a live tick and is charged in full whatever its size. That is why sleeping
through the night and the `skiptime` console command still cost full price.

## What it covers

Anything carrying the vanilla `Fireplace` component: every player-buildable fire, and any
modded fire built on the same component. Numbers ripped from the running game:

| Piece | Prefab | Holds | One fuel | Full tank |
| --- | --- | --- | --- | --- |
| Campfire, iron campfire | `fire_pit`, `fire_pit_iron` | 10 | 1h 23m | 13.9h |
| Bonfire | `bonfire` | 10 | 1h 23m | 13.9h |
| Hearth | `hearth` | 20 | 1h 23m | 27.8h |
| Wood ground torch | `piece_groundtorch_wood` | 4 | 2h 47m | 11.1h |
| Standing brazier | `piece_brazierfloor01` | 5 | 5h 33m | 27.8h |
| Ground torch, blue and green variants | `piece_groundtorch`, `_blue`, `_green` | 6 | 5h 33m | 33.3h |
| Wall torch | `piece_walltorch` | 6 | 5h 33m | 33.3h |

Every column is populated world time, not wall clock. Cooking fires and the wood ground torch
burn wood; the ground torch, wall torch and standing brazier burn resin.

Cooking fires burn four times faster per fuel than torches do, which is why a base goes dark
unevenly. A campfire is empty after about fourteen hours of other people playing, a wall torch
after thirty-three, so the fires you cook and warm yourself on go out first and the lighting
outlasts them.

Not covered:

- The demister ground torch (`piece_groundtorch_mist`). It has no `Fireplace` component and
  burns no fuel.
- Smelters, charcoal kilns, blast furnaces, eitr refineries, ovens and cooking stations. Those
  are separate components with their own clocks, and a smelter finishing while you are away is
  something players want. Worth knowing that a cooking station has the same catch-up behaviour
  and will turn a rack of meat to charcoal over an unload. That is out of scope here.

Nothing refills anything. There is no auto-feeder and no chest access, and a fire that was
nearly empty when you left still goes out. The cap limits what an absence costs, it does not
conjure fuel, so the habit worth having is to top your fires up before you log off.

One side effect worth knowing: a fire that stays lit stays able to spread. `IsBurning` gates
the ignite loop as well as the flame, so coming home to a lit fire is coming home to one that
can still catch what is next to it, where vanilla would have let it go out. Every fire in the
table above has a non-zero ignite radius except the wall torch. It only triggers where the game
already allows cinders, so in practice this is an Ashlands concern rather than a Meadows one.

## Installation

Install through a mod manager, or drop `Vaka.dll` into `BepInEx/plugins`.

Requires [BepInEx 5.4.2350](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
Built against Valheim 1.0.7, BepInEx 5.4.23.5 and Harmony 2.9. BepInEx 5 API only, not
compatible with BepInEx 6.

[Longhouse Core](https://thunderstore.io/c/valheim/p/Ezomic/Longhouse_Core/) is an optional
soft dependency. Vaka runs without it; see Multiplayer for what you lose.

## Configuration

`BepInEx/config/ezomic.valheim.vaka.cfg`, written on first run. Each entry has its full
comment in the file.

| Setting | Section | Default | Effect |
| --- | --- | --- | --- |
| `Enabled` | `[Vaka]` | `true` | Whether the cap applies. Off leaves the plugin loaded and charges vanilla's full bill for every absence, which is useful for telling the two apart without uninstalling. |
| `MaxFuelPerAbsence` | `[Vaka]` | `1` | How much fuel a fire may lose to a single absence, in the units the hover text counts. `0` makes an absence free. A large number restores vanilla. |
| `Verbose` | `[Diagnostics]` | `false` | One log line per fire every time one comes back from being unloaded. See Checking that it works. |

`MaxFuelPerAbsence` is counted in fuel units rather than seconds, so it means the same thing on
a torch burning resin and a hearth burning wood, and on fires added by other mods. It is also
one press of E: refuelling adds exactly one unit per press whatever the fire is. It never
affects a fire you are standing next to, whatever you set it to.

BepInEx writes every entry to disk on first run and the saved value beats a new default in
code. If a later version changes a default, edit the cfg as part of the same update.

## Multiplayer

Everyone needs Vaka, at the same version and build.

Whichever client owns the fireplace's network object does the arithmetic, and ownership moves
every two seconds to whichever player's active area covers the fire. One person without Vaka
walking up to your base pays the absence off the vanilla way, for everybody, with both logs
looking reasonable. Installing it on the server alone does not help: a dedicated server never
sets a reference position, so it is never a candidate owner for a player-built fire and there
is no host simulation to put the cap in.

With Longhouse Core installed, Vaka registers with its version check and the server rejects
clients whose version or build id does not match. The host's `Enabled` and `MaxFuelPerAbsence`
then apply to connected clients in memory, without writing their own config file, and their
values come back on disconnect. `Verbose` stays local to each player.

Without Core, Vaka still caps absences on your own machine, but nothing stops a client that
lacks it from removing the cap for everyone.

## Checking that it works

Hover text draws fuel rounded up to a whole unit, so a fire that quietly lost a third of a log
reads the same as one that lost nothing, and both read the same as a mod that never loaded.

Set `Verbose = true`. Every catch-up writes one line to `BepInEx/LogOutput.log` naming the
fire, how long it was away, what that was worth in fuel and what it was charged, including the
times the cap did not apply. Walk far enough from a fire that its zone unloads, wait a few
minutes, walk back, and either there is a line with numbers in it or the mod is not running.
It stays silent about unlit, switched-off, roofed and infinite-fuel fires, which were never
going to spend anything.

Leaving a world overnight proves nothing in either direction. World time is saved with the
world and only advances while somebody is online, so quitting and coming back bills close to
zero whether the mod is installed or not.

For a conclusive test, use a throwaway character in a throwaway world, because `skiptime`
permanently marks a character as having used cheats:

1. Place a ground torch outdoors on open flat ground. The cover check raycasts upwards from
   just above the torch, so anything solid within roughly a metre overhead stops it burning at
   all, which passes in both arms of the test.
2. `devcommands`, then `setfuel 6`. You cannot reach a full 6 by hand, because refuelling
   refuses once the rounded-up value hits the maximum, so a hand-filled torch sits around 5.02.
3. Walk more than 150m away so the zone unloads. Run `skiptime 200000`. Walk back.
4. With `Enabled = false` the torch reads 0/6 and is dark. With it `true` it reads 5/6 and is
   still lit.

Use at least three times the cap. `skiptime 20000` bills exactly one fuel either way, which
looks like a failure and is not. Do one skip and let one tick land: several skips in a row are
several separate absences, each capped on its own, which drains the torch and reads as no cap
at all. Running `skiptime` while standing at the fire shows no difference, because that is not
an absence.

## Troubleshooting

**Nothing in the log and fires still go out.** Check the startup lines. Vaka patches a private
method matched by name, and if `Fireplace.GetTimeSinceLastUpdate` is not in your build of the
game it logs an error saying it is doing nothing and fires burn the vanilla way. A game update
is the likely cause.

**A config change had no effect.** BepInEx keeps the value saved on first run. Edit the cfg
rather than expecting a new default to apply.

**Fires still drain on a server.** Confirm every connected player has Vaka. Without Longhouse
Core nothing enforces that, and one client without it is enough.

**Nothing is logged for a short trip.** Nothing is trimmed until the bill passes one fuel,
which is an hour and a half of populated world time for a cooking fire and five and a half
hours for a torch. Under that, vanilla is already charging less than the cap allows.

## Bug reports

Discord, or the [issue tracker](https://github.com/Ezomic/valheim-vaka/issues). Useful to
attach:

- `BepInEx/LogOutput.log`, ideally from a session with `Verbose = true`.
- `AppData/LocalLow/IronGate/Valheim/Player.log` if a vanilla mechanic broke. Gameplay
  exceptions land there rather than in LogOutput.
- Whether you were on a server or in single player, and whether Longhouse Core was installed.
- `BepInEx/config/ezomic.valheim.vaka.cfg`.
- The prefab name of the fire, if it is one specific piece behaving oddly.

## Discord

[discord.gg/hJzAVaZ5wb](https://discord.gg/hJzAVaZ5wb) for mod information, updates, support,
bug reports and compatibility questions.

## Server

There's also a small EU server running the pack if you want somewhere to play. Details are in
the Discord.

## Part of Longhouse

Vaka ships in the [Longhouse](https://thunderstore.io/c/valheim/p/Ezomic/Longhouse/) modpack,
which pins exact versions of its member mods. It behaves the same installed on its own.

## Changelog

[CHANGELOG.md](CHANGELOG.md) carries the release history and the reasoning behind the design
calls, including why the mod was renamed from Ember before release.

## Licence

MIT, see [LICENSE](LICENSE). By Robbin Thijssen, Thijssen Software.
