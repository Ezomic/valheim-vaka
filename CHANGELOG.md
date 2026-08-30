# Changelog

## 0.1.0 - unreleased

First version. A fire loses at most one fuel to an absence, however long the absence was.

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
own rate - a wall torch is 20,000 seconds a resin, a wood ground torch 10,000 - and one
number in seconds would be a whole torch for one and a rounding error for another. In fuel it
means the same thing everywhere, including on fires other mods add.

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

### Verbose reports the catch-ups it did not cap, on purpose

Fuel is drawn as `Mathf.Ceil(fuel)` out of the maximum, so anything under a whole unit is
invisible in the hover text. That makes three different states look identical from in front of
a fire: the cap worked, nothing had burned down anyway, and the patch never applied. Logging
only the capped ones would have preserved exactly that ambiguity.

So `Verbose` writes a line on every catch-up, capped or not, and the line carries the span,
what it was worth in fuel and what was actually charged. Walk out of a zone, wait, walk back,
and either there is a line or the mod is not running.

### What has actually been run

**Nothing in game.** It compiles clean and deploys to its own `testprofile`, and that is the
whole of it. It is deliberately in neither `build-all.ps1` nor `server.ps1`.

What is verified, and how: the mechanism, the single call site, the return type and the
absence of any other timed fuel drain on a `Fireplace` come from a full ILSpy decompile of the
installed `assembly_valheim.dll`. The Harmony details - that a string-named private target
resolves through `DeclaredMethod`, that `ref double` is required to write a return value and
that a plain `double __result` would silently discard the write - come from reading the
emitter in the installed `0Harmony.dll`. The torch numbers come from devkit rips already in
this profile.

Not exercised at all: any fire in a running game, the cap on a real absence, the `Verbose`
line, multiplayer of any kind, and the hearth and campfire numbers, which have not been
ripped and are the fires most likely to be the ones actually going out.
