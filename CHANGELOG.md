# Changelog

Notable changes to Vaka. Format follows [Keep a Changelog](https://keepachangelog.com),
and the mod uses [semantic versioning](https://semver.org).

## [1.1.0] - 2026-09-30

Fires that burn resin or coal last twice as long.

### Added

- `LightFuels` and `LightFuelMultiplier`. A fire whose fuel item is on the list burns each unit
  that many times as long, whether you are at it or not. The defaults are `Resin, Coal` and
  `2`, so a wall torch holds a resin for 11h 7m of world time instead of 5h 33m. Cooking fires
  burn wood and are left alone.
- A log line each time you enter a world, naming the fires the light rule applies to, grouped
  by fuel. A fuel on the list that no fire burns is named too. With `Verbose` on there is also
  a list of every fireplace in the game with its fuel and burn rate. Every player gets the
  line, whoever is standing at the fires. A dedicated server does not write it.

### Changed

- `Enabled` switches off both rules now, not only the absence cap.

The rule reads each fire's fuel item rather than a list of pieces, so a modded torch that burns
resin is covered as well. In the game as it stands, the log line puts the wall torch, the
ground torch and the wood ground torch on resin, along with `Candle_resin`,
`piece_snowlantern`, `piece_jackoturnip` and `CastleKit_groundtorch_unlit`. The standing and
hanging braziers are on coal. The blue and green torches burn neither and keep their vanilla
rate. The 1.0 README said the wood
ground torch burns wood. It does not.

A cooking station or cauldron counts any fire with a burning area under it, not only a
campfire. Both braziers carry one, so cooking over a brazier would run on the longer fuse.
Nobody has tried it. The log line names every fire where that could happen.

It stacks with the cap in fuel. An absence still costs a torch at most one resin. The torch
just has to be left twice as long before the bill gets there.

Both settings are new, so an existing config picks up the defaults without editing. With
Longhouse Core the host's values apply to everyone, like the cap's. A player without Vaka who
owns a torch burns it at the vanilla rate for as long as they own it.

## [1.0.1] - 2026-09-12

### Changed

- Rewritten README. Same mod, clearer documentation: what it does and how to install it come
  first, then configuration, multiplayer behaviour, compatibility and troubleshooting. Every
  config table was checked against the plugin's own Config.Bind calls, so the settings,
  sections and defaults listed are the ones actually bound. No code changed in this release.

## [1.0.0] - 2026-09-04

First release. A fire loses at most one fuel to an absence, however long the absence was.

### Named Vaka, having been built as Ember

*Vaka* is the Old Norse for a vigil, a watch kept through a night nobody else is awake for,
which is what the mod asks of a fire. It was written under the name Ember and renamed before
any of it was published.

The rename was not taste. **Ember collides on Thunderstore** - there is already a Valheim
package `Ember/ember`, plus `ember_server` and `Embers_of_Niflheim` - and outside Valheim the
word belongs to Ember.js and to Warframe, which would have hurt the repo and the C# namespace
too. Checking that took some care: Thunderstore's web search silently ignores its `?q=`
parameter and returns the default popular listing, so the obvious check reports a false
all-clear. The package index was downloaded whole and grepped instead, and the eight names
shortlisted were all verified clear that way.

The name also stopped describing the mod. Ember is a fire word, and this is not a mod about
fire - it never changes what a fire burns while you are standing at it, and its entire subject
is the unloaded zone. It sat too close to Kynda, which is already Old Norse for "to kindle".
Vaka names the absence instead.

Renaming cost nothing because nothing had shipped and the mod registers no prefabs, so no ZDO
is keyed on a name that would stop resolving - the cost Kynda paid deliberately when its test
pieces died. The config section moved from `[Ember]` to `[Vaka]` for the same reason: renaming
a section silently resets every value under it, which would matter if anyone but the author
had a config file, and nobody does.

### The line this sits on

> It changes what an absence costs. It never changes what burning costs.

While you are near a fire it consumes exactly what vanilla says it consumes, to the same
decimal, and this mod can be proven to do nothing at all: a live update measures about two
seconds against a cap that is hours away. Everything here happens on the one update that
lands when your base loads back in.

### Why it is a cap and not a longer fuse

The complaint that started this was a day away and a base of cold fires with nobody having
been near them, and the obvious fix is to make fuel last longer. That is wrong twice. It
makes a fire cheaper while you are stood at it cooking, which was never the complaint, and
it only moves the deadline - a longer trip next time arrives at the same cold base.

What is actually wrong is what a fireplace is charged for. `Fireplace.UpdateFireplace` bills
against the world clock by diffing `ZNet.GetTime()` against an `s_lastTime` stamp on the ZDO,
and while the zone is unloaded nothing re-stamps it. The first update after the zone returns
therefore pays for the entire gap at once, and whether a person was present is not part of
the sum at any point.

The clock is the world's rather than the wall's, which is why the symptom is confusing.
`ZNet.UpdateNetTime` only advances on a server while `GetNrOfPlayers() > 0`, and the value is
saved into the world file, so an empty server is frozen and a singleplayer world resumes
where it stopped. What drains a base is the hours other people were online elsewhere on the
map, or the hours you spent across it yourself.

### One fuel, and what it does not buy

`MaxFuelPerAbsence` is counted in fuel units rather than seconds, because every fire has its
own rate and one number in seconds would be four hours of a hearth and a fifth of a torch.
Ripped from this install:

| Fire | Holds | One fuel | Full tank |
| --- | --- | --- | --- |
| Campfire, iron campfire | 10 wood | 1h 23m | 13.9h |
| Bonfire | 10 wood | 1h 23m | 13.9h |
| Hearth | 20 wood | 1h 23m | 27.8h |
| Wood ground torch | 4 wood | 2h 47m | 11.1h |
| Standing brazier | 5 resin | 5h 33m | 27.8h |
| Wall torch, ground torch | 6 resin | 5h 33m | 33.3h |

Two things fall out of that. **Cooking fires burn four times faster per fuel than torches**,
so a campfire empties after fourteen hours of other people playing while a wall torch takes
thirty-three - which is why a base goes dark unevenly and the fires you cook on go first. And
the floor is 5,000 seconds, so no fire has a rate short enough for a cap of one fuel to come
anywhere near a two-second tick. The worry that a short-rate prefab would make the cap throttle
ordinary burning does not exist in this game.

The unit also happens to be the one the player acts in, which is the argument against
expressing the cap as a fraction of the tank instead. `RPC_AddFuel` adds exactly `1f` per
press whatever the fire is, so "an absence costs at most one press of E" is the same promise
on all six, even though one fuel is a twentieth of a hearth and a quarter of a wood ground
torch.

One unit is a real price and it is paid every time the fire comes back into play. A fire left
nearly empty still goes out; the cap limits what an absence costs, it does not conjure fuel.
Bank your fires before you log off and they will be lit when you get back.

### Everyone needs it, and the reason is ownership

`Requirement.Everyone`, and the argument is structural rather than cautious. The drain runs
under `m_nview.IsOwner()` and writes `ZDOVars.s_fuel`, which is shared world state, and
`ZDOMan.ReleaseNearbyZDOS` reassigns ownership every two seconds to whichever peer's active
area covers the fire. One player without the plugin walking up to your base takes the fire
over and pays the absence off the vanilla way, for everybody, silently.

Handing it to the host instead is not available: a dedicated server never calls
`ZNet.SetReferencePosition` - every caller is player code - so its reference position stays at
the world origin and it is never a candidate owner for a player-built fire.

### The patch, and why it is that one method

A postfix on the private `Fireplace.GetTimeSinceLastUpdate`, clamping the `double` it returns.
That method has exactly one caller, inside vanilla's own `IsOwner() && m_secPerFuel > 0f`
guard, and its value is used for nothing but the fuel subtraction - so the patch inherits both
guards for free and cannot alter anything else.

It also already re-stamps `s_lastTime` to now before returning, which means the trimmed
seconds are written off rather than deferred to the next update. That is the behaviour a cap
wants, but it is a consequence of where the patch sits rather than something it chose, so it
is worth stating: a postfix here could not bank the remainder even if that were wanted.

A live tick is never touched, whatever the cap says. Without that guard, setting
`MaxFuelPerAbsence` to 0 would clamp every two-second tick to zero as well, and "an absence is
free" would quietly mean "fires never burn at all".

### An absence is the fire not having run, never a big number

The first version decided what counted as an absence by the size of the gap: anything longer
than three update intervals. That reads as correct and is not. Sleeping runs
`EnvMan.UpdateTimeSkip`, which pushes the world clock forward at roughly fifty times normal
for a dozen real seconds, and `skiptime` jumps it instantly - so both arrive as an enormous
gap with the zone loaded and the fire ticking the whole time. A size test forgives them, which
would have made sleeping through the night a free burn on any fire with a short enough rate,
and it would never have shown up on a torch because a torch's cap is five and a half hours.

So the test is now the component's own history. A `ConditionalWeakTable` records which
fireplaces this machine has already billed; the first bill for an instance is the one paying
for however long the thing was not running, and every later call is a live tick that is never
touched however large it is. ZNetScene destroys and rebuilds a fireplace with its zone, so a
new instance is exactly a return from an absence.

It also closed a hole nobody had noticed: ownership is granted only inside a player's own 64m
zone while objects are instantiated across 192m, so a fire 40m away across a zone line loses
and regains its owner without ever being destroyed. Under the size test that was a fresh
capped "absence" every crossing, and a patrol loop past your own base would have kept it lit
for one wood a lap. Under the history test the instance was never destroyed, so it is a live
tick and is charged in full.

### Verbose reports the catch-ups it did not cap, on purpose

Fuel is drawn as `Mathf.Ceil(fuel)` out of the maximum, so anything under a whole unit is
invisible in the hover text. That makes three different states look identical from in front of
a fire: the cap worked, nothing had burned down anyway, and the patch never applied. Logging
only the capped ones would have preserved exactly that ambiguity.

So `Verbose` writes a line on every catch-up, capped or not, and the line carries the span,
what it was worth in fuel and what was actually charged. Walk out of a zone, wait, walk back,
and either there is a line or the mod is not running.

It stays silent about fires that were never going to spend anything, which is the other half
of being truthful. `GetTimeSinceLastUpdate` is called above vanilla's
`IsBurning() && !m_infiniteFuel && state == 1` gate, so an unlit torch, one somebody switched
off, one under a roof and an infinite-fuel brazier all arrive here with a huge gap and nothing
at stake. Reporting those would claim savings that never existed - the same kind of lie the
flag exists to prevent.

### A NaN in the config would have killed a fire permanently

Reachable only from a hand-edited cfg, but the damage was to saved world data. BepInEx parses
a float with `NumberFormatInfo.InvariantInfo`, whose `NaNSymbol` is the literal `NaN`. That
would have multiplied through to a NaN cap, and vanilla's subtraction floors fuel at zero
without ever testing for NaN - so the fire's stored fuel becomes NaN, is written into the
world, fails every `> 0` test forever, and survives refuelling because `Mathf.Clamp` leaves
NaN alone. One bad character, one dead fire, no way back without a console command.

`CapSeconds` now refuses a non-finite rate or amount and hands the fire back to vanilla. The
comparison it feeds was already NaN-safe by accident; being safe on purpose is cheaper than
finding out which.

### What has actually been run

**The cap works, measured in game on 2026-09-04.** A campfire at 10 fuel, teleported away
from so the zone unloaded, `skiptime 20000`, teleported back:

    fire_pit was away 5,6h of world time, worth 4,00 fuel. Charged 83,3m, 1,00 fuel.

Every number in that line is exact against the rip. 5.6h is the 20,000 second skip; 4.00 fuel
is 20,000 divided by the campfire's 5,000 seconds a fuel; 83.3 minutes is 5,000 seconds, which
is the cap to the second. One line, so nothing double-billed on re-entry, and no exception in
either `LogOutput.log` or `Player.log`.

That one line is both arms of the test, because it reports what vanilla would have charged
alongside what was charged - so the `Enabled = false` control run was not needed to make the
claim. It is deliberately in neither `build-all.ps1` nor `server.ps1`.

What is verified, and how: the mechanism, the single call site, the return type and the
absence of any other timed fuel drain on a `Fireplace` come from a full ILSpy decompile of the
installed `assembly_valheim.dll`. The Harmony details - that a string-named private target
resolves through `DeclaredMethod`, that `ref double` is required to write a return value and
that a plain `double __result` would silently discard the write - come from reading the
emitter in the installed `0Harmony.dll`. The torch numbers come from devkit rips already in
this profile.

Every fireplace prefab in the table above was ripped from the running game on 2026-08-30,
which retired the one open question the design hung on: whether some fire had a rate short
enough that a one-fuel cap would clamp ordinary ticks. None does - the floor is 5,000 seconds.
No code changed as a result; the numbers only confirmed it.

**The discriminator was then checked in the other direction, in the same session.** The same
skip run while standing at the fire, with it loaded and ticking the whole time:

| Test | Fuel | Vaka line | |
| --- | --- | --- | --- |
| `skiptime 20000`, zone unloaded | 10 to 9 | logged, 4.00 charged as 1.00 | capped |
| `skiptime 20000`, stood at the fire | 10 to 6 | none | vanilla, untouched |

6/10 is exactly 10 minus 20,000 over 5,000, so the present case is billed at full vanilla
rate, and the log still carried exactly one catch-up line for the whole session. That is the
half that stops a night's sleep being free burn, and it is the half the first cut of this mod
got wrong. Both directions now hold: an absence is the fire not having run, and a large gap on
its own is not enough to trigger one.

Still untouched: multiplayer of any kind, ownership moving between clients, the `Verbose` gate
that is meant to stay silent on unlit and infinite-fuel fires, and every fire type except the
campfire. None of those block calling the mechanism proven.

The README carries the recipe, along with why the obvious test - leave it overnight, come
back, look - proves nothing at all in either direction.

### What this is worth, stated at the right scale

Vanilla already bounds the loss: fuel floors at zero, so the most an absence of any length can
ever cost is the fire's current fuel, and for a torch that is at most six wood. This turns
"came back to a dark torch" into "came back one wood down". The wood was never the point - the
lap of the base pressing E, and the warmth and Rested source being off, are what an outage
actually costs, and that is the thing being bought.

It is also narrower than its own name suggests. A solo logout was never billed at all, because
the world clock freezes with the fire, and an empty dedicated server is frozen too. What is
left is the two cases that are real on a shared server: other people playing while you were
away, and your own long walk across the map.
