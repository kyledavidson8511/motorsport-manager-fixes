# Motorsport Manager Fixes & QoL

Fixes for bugs in the base game, plus a few quality-of-life additions, for the 2016 Steam version of Motorsport Manager. It's a Unity Mod Manager mod. Each fix logs what it changes, once, to the UMM log.

**Download:** [Nexus Mods](https://www.nexusmods.com/motorsportmanager/mods/31) · [GitHub releases](https://github.com/kyledavidson8511/motorsport-manager-fixes/releases)

## Fixes

**A driver's car follows his race seat.** The game also keeps a car number on each driver, and swaps between race and reserve drivers can leave it out of date. When that happens, Part Fitting shows the other car's parts on both panels. Driver morale is affected too: "my car vs my team-mate's" ends up comparing the wrong cars, so a Number 1 driver in the better car can complain he's been given the worse one. The race itself always used the seats; now Part Fitting and morale do as well. Genuine reserves standing in for an injured driver aren't affected.

**Driver car opinions stay current.** The game recalculates a driver's opinion of his car only when a part is fitted to *his* car, but his "vs my team-mate's car" opinion depends on both. Both drivers' opinions are now recalculated when Part Fitting opens, and whenever either car's parts change. Morale is still applied when the game normally applies it (travelling to the event).

## Quality of life

**Browse the scouting list from a detail screen.** Open a driver, designer or mechanic from Scouting, and the left/right arrows beside the status label step through the list as you had it filtered and sorted. The game normally hides these arrows for free agents. The left and right arrow keys work too, and Back still returns to Scouting.

**Marketability column in Scouting.** When listing drivers, Marketability replaces the Nationality column. Click the header to sort; the first click puts the highest first. Unscouted drivers show "?" and sort to the bottom. Designers and mechanics keep Nationality.

**Renew All on the Pit Crew screen.** A button beside Auto Manage Option renews every pit crew contract the game's own Renew button would allow, in one click (no fee, same as renewing one by one). Crew who are retiring or past peak age are left alone. The button shows how many contracts it will renew, and greys out when none need it.

## Install

1. Install Unity Mod Manager and set it up for Motorsport Manager (tested with UMM 0.33.2).
2. Download `MMFixes-1.3.1.zip` from [Nexus Mods](https://www.nexusmods.com/motorsportmanager/mods/31) or the [latest GitHub release](https://github.com/kyledavidson8511/motorsport-manager-fixes/releases/latest).
3. Extract the `MMFixes` folder to `Steam/steamapps/common/Motorsport Manager/Mods/`.
4. Start the game, press Ctrl+F10, and check **Motorsport Manager Fixes & QoL** is active.

It doesn't modify game files or add anything to your save, so you can add or remove it at any time. Endurance series cars aren't affected by the seat fixes.

## Build from source

`src/mm-fixes/` targets .NET 3.5. Build against your local `MM_Data/Managed` assemblies (`dotnet build -c Release -p:GameManaged=<path to MM_Data/Managed>`), with Harmony in `src/mm-fixes/lib/`. Game assemblies and Harmony aren't included here.

## Feedback

Open an issue with the screen, what you expected, and the `[MMFixes]` lines from the UMM log.
