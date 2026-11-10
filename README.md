# CasualtiesSize

Change how big players are in Casualties: Unknown, on your own or together with other players.

A size changes what a body can do:

- Small bodies fall more slowly, take less fall damage, weigh less when carried, and take more damage from larger players' hits.
- Large bodies hit harder, reach further, dig faster and carry more.
- Everything between two bodies depends on the ratio of their sizes, so it works for any pair: small and normal, normal and large, small and large.
- Stature stays: a character's own size is multiplied by the factor you set.

## Requirements

- Casualties: Unknown with BepInEx 5.4.23.5
- For multiplayer: the Together co-op mod, version 5.4.1 or newer, installed by every player
- Every player in a multiplayer game needs this plugin. A player without it sees no size change.

## Install

1. Close the game.
2. Unzip the release into the game folder (the one that contains `CasualtiesUnknown.exe`), so that `BepInEx/plugins/CasualtiesSize/CasualtiesSize.dll` exists.
3. Start the game.

To remove it, delete the folder `BepInEx/plugins/CasualtiesSize`.

## Commands

Open the console with the ` key.

| Command | Effect |
|---|---|
| `size <target> <factor>` | Set a size, for example `size @m 2`, `size Bob 0.5`, `size @a 1.5`. Also `x0.5` and `150%`. |
| `size <target> reset` | Back to normal size. |
| `size <target>` | Show one size. |
| `sizes` | List everyone who is not normal size. |
| `sizerule [name [value]]` | Show or change the size rules. In multiplayer only the host can change them. |
| `pickup <holder> <target>` | A body at least 1.5 times larger lifts another. Experimental. |
| `eat <eater> <target>` | A body at least 2 times larger swallows another. Experimental. |
| `release <target>` | Let a held or swallowed body go. |

Targets: `@m` yourself, `@a` everyone, `@o` everyone else, `@r` a random player, `@c` the player at your cursor, `id:N`, or a name.

Size limits are 0.25 to 4; anything outside is refused with a message.

By default anyone may change anyone's size. The host can restrict that with `sizerule SizePermission 1` (players change only themselves) or `2` (only the host changes sizes). `sizerule` lists every other rule, such as the strength of damage scaling and the size ratios needed to stomp, pick up or eat.

A held or swallowed player keeps their camera and console and presses jump to struggle free. A swallowed player slowly takes damage and is released after a time limit.

Sizes are kept until the game is closed or everyone leaves. Using `size` turns achievements off until the game is restarted, as with the game's own cheat commands.

## Known limitations

- Eating, picking up and stomping other players are experimental.
- Camera zoom for large and small players may not apply.
- Footstep and voice pitch do not change with size.
- Sizes are not saved with a save file.

## Credits

Casualties: Unknown is made by its developers. BepInEx and Harmony are made by their authors. Multiplayer support is built on the Together co-op mod.

## Development

Build with `dotnet build -c Release`, install with `python tools/install.py`, and make a release zip with `python tools/build_package.py` (it keeps any earlier zip of the same name). See [docs/dev/TESTING.md](docs/dev/TESTING.md) for tests and [docs/dev/DESIGN.md](docs/dev/DESIGN.md) for the design, hook points and network protocol.
