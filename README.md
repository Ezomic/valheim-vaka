# Ember

A fire loses at most one fuel while you are away, however long you were away.

An ember is the part of a fire that survives being left alone, and the thing you blow on
when you get back rather than rebuild. That is the whole mod: come home to a fire that is
one log down, not a base of cold ash and a bill in resin.

## Why

The complaint is a day away and every fire out, with nobody having been anywhere near them.
The natural reading is that fuel burns too fast. It does not. What is wrong is what a
fireplace gets charged for.

A fire does not burn in the background. While its zone is loaded, `Fireplace.UpdateFireplace`
runs every two seconds and asks how long it has been since the last run, by diffing the world
clock against a timestamp on the fire's own network object. When the zone is unloaded nothing
runs and nothing re-stamps that timestamp, so the gap keeps growing. The first update after
your base loads back in pays for the whole gap at once. Whether a person was standing there
is not part of the sum at any point, and a base of thirty torches is thirty of those bills
landing in the second you walk through the door.

The clock behind it is the world's, not the wall's. A dedicated server only advances world
time while at least one player is connected, and it is saved into the world file, so an empty
server is frozen and a singleplayer world resumes exactly where it stopped. What actually
drains your base is every hour somebody else was online somewhere else on the map. In
singleplayer it is every hour you spent across the map yourself.

The obvious fix is to make fuel last longer, and it is wrong twice over. It makes a fire
cheaper while you are stood at it cooking, which was never the complaint, and it only moves
the deadline: a longer trip next time arrives at the same cold base. Ember caps the bill
instead. What an absence costs stops depending on how long it was, and what a fire costs
while you are using it does not change at all.

For scale, from a rip of this install: a wall torch and a ground torch hold six resin at
20,000 seconds each, so a full one is 33 hours of players-online time. A wood ground torch is
four at 10,000, so 11 hours. Those are the numbers an absence is being billed against.

## Using it

Nothing. There is no key, no piece and no menu. Fires burn exactly as they always did while
you are near them, and an absence costs at most one fuel instead of all of it.

The one habit worth having is to top your fires up before you log off. The cap limits what an
absence costs; it does not conjure fuel. A fire left with half a log in it still goes out.

## What it does not do

It is not an auto-feeder. Nothing refills anything, nothing reaches into a chest, and the
walk to the fire with an armful of wood is still the walk to the fire with an armful of wood.

It is not an infinite fire. One unit off the top is a real price, and it is charged each time
the fire comes back into play rather than once per calendar day. On a busy server, several
people passing your base at different hours is several of those, because each of them loads
the zone afresh.

It does not change burning. Every second you spend in front of a fire is charged exactly what
vanilla charges, to the same decimal. While you are there this mod provably does nothing: a
live update measures about two seconds and the cap is hours away.

It does not touch smelters, kilns, blast furnaces, ovens or cooking stations. Those are
different components with their own clocks on their own keys, and a smelter finishing while
you are away is a thing players want rather than a thing to fix.

## Telling whether it is working

Harder than it should be, and worth knowing before you go looking. A fire's hover text draws
its fuel rounded up to a whole unit, so a fire that quietly lost a third of a log reads the
same as one that lost nothing, and both read the same as a mod that never loaded.

Set `Verbose` to `true` in the config. Every time a fire comes back from being unloaded it
writes one line to `BepInEx/LogOutput.log` naming the fire, how long it was away, what that
was worth in fuel and what it was actually charged - including the times the cap did not
apply, which is the half that tells you the patch is live at all.

The quick check is to walk far enough from a fire that its zone unloads, wait a few minutes,
and walk back. Either there is a line with numbers in it or the mod is not running.

## Installing

Drop `Ember.dll` into `BepInEx/plugins`. BepInEx 5.4.23.3, and nothing else is required.

## Settings

The file is `BepInEx/config/ezomic.valheim.ember.cfg`. Every setting has a comment above it,
so the file explains itself. The one worth knowing about is `MaxFuelPerAbsence`, which is the
whole mod: one unit by default, `0` to make an absence free, and a large number to put vanilla
back. It is counted in the units the fire's own hover text counts, so it means the same thing
on a torch burning resin and a hearth burning wood, and on fires added by other mods.

Changing a default in a new version does nothing on a machine that has already run the mod.
BepInEx writes every entry on first run and the saved value wins.

## Multiplayer

**Everyone needs it.** The server refuses a client that does not have it, at the same build.

That is stricter than it looks like it should be, since this registers no prefab and invents
no saved data, so nothing is destroyed by a client that lacks it. The reason is who does the
arithmetic. The bill is charged by whichever client owns the fireplace's network object, and
that is whoever happens to be nearest: the server reassigns ownership every two seconds to
whichever player's active area covers the fire. One person walking up to your base without
this plugin pays the absence off the vanilla way, and the cap is gone for everybody, silently,
with both logs looking reasonable.

Handing it to the server instead is not an option. A dedicated server never sets a reference
position of its own, so it is never a candidate owner for a player-built fire. There is no
host simulation to put this in, which is why "the host needs it" is not one of the answers
here.

If [Core](https://github.com/Ezomic/valheim-core) is installed, this mod registers with its
version gate and the host's settings apply to everyone connected to it. Your own values are
restored when you disconnect. Without Core the mod still runs; what is lost is the
enforcement, which here is most of the point.

## Licence

MIT. See `LICENSE`.
