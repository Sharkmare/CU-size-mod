# CasualtiesSize

A BepInEx 5 plugin for Casualties: Unknown. It sets body size per player, in singleplayer and, with the Together mod, in multiplayer, and makes melee damage, digging, reach, carry capacity, stomping, picking up and eating depend on the ratio of two bodies' sizes. Design and source citations: [DESIGN.md](DESIGN.md).

It is separate from Together and works without it. Debug output is off.

## Install

```bash
dotnet build -c Release
python tools/install.py            # refuses while the game runs
python tools/install.py --remove
```

The plugin lands in `BepInEx/plugins/CasualtiesSize/`. The build reads the game's `Managed` folder, `BepInEx/core` and, for the multiplayer bridge, `BepInEx/plugins/Together/CasualtiesMP.dll` and `LiteNetLib.dll` (override with `-p:GameDir=...` or `-p:TogetherDir=...`). Together is loaded only if present; without it the bridge is never touched.

## Console

| Command | Effect |
|---|---|
| `size <target> <factor>` | Set a size: `2`, `x0.5`, `150%`. It multiplies the character's own stature. |
| `size <target> reset` | Back to the prefab size. |
| `size <target>` | Show one size. |
| `sizes` | List every body that is not size 1. |
| `sizerule [name [value]]` | Show or set a rule. Host only in multiplayer. |
| `pickup <holder> <target>` | A body at least `PickupRatio` times larger lifts another. |
| `eat <eater> <target>` | A body at least `EatRatio` times larger swallows another (rule `EatEnabled`, default on). |
| `release <target>` | Let a held or swallowed body go. |

Targets are Together's: `@m` you, `@a` everyone, `@o` others, `@r` and `@r:N` random, `@c` at the cursor, `id:N`, `body:N`, `team:`, a name or prefix. In singleplayer every macro is the one player body. Using `size` switches achievements off until restart, like the game's own cheat commands.

## Rules

`sizerule` lists every rule with its default and bounds; they are defined in `src/Core/SizeRules.cs`. The host's values reach clients with the welcome packet and on every change.

## Held and swallowed

A held body is pinned to the holder's hand. A swallowed body is pinned to the holder's torso, drawn faded, and loses skin and muscle on its vital limbs each second (`EatDigestionDps` times the size ratio) while the eater gains hunger. The captured player keeps their camera and console and presses jump to struggle: `EatEscapeStruggles` presses, decaying one per second, break free. A swallowed body is also released after `EatMaxSeconds`, and when the eater falls unconscious or dies.

## Multiplayer

The host decides. A client without the plugin is never sent anything and sees no change to sizes; a host without the plugin leaves a client's `size` with the message that no answer came. A protocol mismatch makes the client refuse sizes and say so. Details in DESIGN.md.

## Tests

```bash
cd tests/CoreTests && dotnet run -c Release     # offline: formulas, rules, parsing, wire, capture timing
python tests/pair/test_size_pair.py               # needs an idle machine: host and client game from the harness
```

The pair test installs nothing; it runs the plugin already installed in the game folder (and a copy of the folder for the client). Run it only while no `.game.lock` exists. It follows the harness rules in the README of the sibling `CU TA Harness` folder (set `CU_TA_HARNESS` to use another location).
