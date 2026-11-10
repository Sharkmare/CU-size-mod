# CasualtiesSize design

A separate BepInEx 5 plugin (`CasualtiesSize`, GUID `hive.casualties.size`) that sets body size per player and makes combat, movement, carrying and eating depend on the ratio of two sizes. Written from the decompiled game in `reference/decompiled/` and the Together fork in `C:/Hive/casualties-together`. Every claim below names the file it came from; items that only a running game can settle are marked **Unverified** and each has a proof listed in README.md.

## What the game gives us

| Fact | Source |
|---|---|
| No runtime size system. `charType` 0 Experiment, 1 Milky, 2 Dune. | `Body.cs:284` |
| A body has two modes. Standing: limbs are children of the Body, `rb.simulated = false`, positions copied from the animator every frame (`Body.cs:3790-3800`), the Body's own `Rigidbody2D` and `BoxCollider2D col` carry the collision. Ragdoll: `Ragdoll()` sets every limb `rb.simulated = true`, Body `rb.mass = 0.01`, `col.enabled = false` (`Body.cs:2158-2172`). `Stand` sets limbs unsimulated and `rb.mass = baseMass` (`Body.cs:2130`). | `Body.cs` |
| `col.size` and `col.offset` are fed straight into world-space casts (`Body.cs:2563`, `3130-3132`) and `col.size = Lerp(origColSize, ..)` runs every frame (`Body.cs:3667`); `origColSize` is a **private** field set once in `Awake` (`Body.cs:101`, `1493`) and used in world space (`2133`, `2189`, `2205`). Scaling the Body transform would therefore make colliders `s` times bigger than every ground check believes. The Body transform stays at its own flip scale; size is applied below it. | `Body.cs` |
| Limbs are children of the Body in both modes (no `SetParent` on limbs in `Body.cs`), their standing local position is `animator offset * heightMultiplier` (`Body.cs:3795`), and `heightMultiplier` is serialized per prefab. Multiplying `heightMultiplier` and each limb's local scale by `s` scales the pose, sprites, limb colliders and joint anchors together; multiplying `col.size`, `col.offset` and `origColSize` by `s` scales the standing collider. | `Body.cs:3795` |
| Limb mass is `baseMass` assigned in `Limb.Awake` (`Limb.cs:586`); `Body.baseMass` is the sum (`Body.cs:1493` area). | `Limb.cs`, `Body.cs` |
| Facing flip is `localScale = (-x, 1, 1)` (`Body.cs:1617`): it resets y to 1, so a size on the Body transform is overwritten on every turn and must be re-asserted. | `Body.cs:1617` |
| The weight display writes each limb's `localScale = (1 + weight.., 1, 1)` (`Body.cs:4157`) and held items counter-scale by `1/limb.x` (`Body.cs:1823`, `4163`). The write resets y and z to 1, so a limb size must be re-asserted after it; a held item is parented under the limb and inherits its scale, so it is counter-scaled to stay its normal world size. | `Body.cs` |
| `heightMultiplier` multiplies the standing limb offsets (`Body.cs:3795`) and is serialized per prefab; this is the likely carrier of Milky's stature. Dune and Milky keep it untouched, the size factor sits on top. | `Body.cs:522`, `3795` |
| Melee, digging and animal hits all go through `Body.Attack(AttackInfo, slot)` (`Body.cs:2321`): `atk.damage` for animals, `atk.structuralDamage * num2` for blocks via `DamageBlock` (`Body.cs:2455`) and buildings, `atk.distance` for reach. `AttackInfo` is a fresh object per swing, so mutating it is safe. | `Body.cs:2321-2470`, `AttackInfo.cs` |
| Carry limit: `overEncumberance = Clamp01(totalEncumberance / maxEncumberance - 1)` recomputed in the `Update` branch at `Body.cs:4282-4284`. | `Body.cs` |
| Eating: `Body.Eat(hungerAmount, weightGain, foodType)` (`Body.cs:2787`), `eatTime`, Milky halves foodType 1, Dune vomits foodType 2. Dune cannibal and carnivore rules live in `Body.Eat` and `CorpseScript.cs`. | `Body.cs`, `CorpseScript.cs` |
| Commands: `ConsoleScript.Commands` is a `List<Command>`, filled in `RegisterAllCommands` (`ConsoleScript.cs:505`, `heal` at 518, `spawn` at 549). A command is `new Command(name, description, Action<string[]>, autofill, (shortDesc, longDesc)...)`. Cheat commands call `CheckForCheats()` first. | `ConsoleScript.cs`, `Command.cs` |

## What Together gives us

| Fact | Source |
|---|---|
| Named custom messages. `Multiplayer.CreateNamedWriter(string)`, `RegisterCustomServerReceiver(name, Action<ScavPlayer, NetDataReader>)`, `RegisterCustomClientReceiver(name, Action<NetDataReader>)`. An unregistered hash is dropped without an error on both sides, which is the mismatch safety. | `ScavMultiplayer.cs:2064-2146` |
| Sending: `Net.Client_Send(DeliveryMethod, NetDataWriter)`, `Net.Server_SendToClients(delivery, writer, IEnumerable<knetid>)`, `Net.Server_SendTo(delivery, writer, knetid)`; `Net.IsRunning`, `IsServer`, `IsClient`. | `Net/Net.cs:206-214`, `810-890` |
| Target macros: `Con.GetPlayersForCommand(name, caller, onlyOne, allowMacros)` handles `@m @a @o @r @r:N @c`, `body:N`, `team:`, `id:N`, exact and prefix names. | `Content/ConsoleCommands.cs` `GetPlayersForCommand` |
| A client's console is local; cheat commands reach the host only in admin mode through `SERVER_Console_TryExecuteCommand`. So `size` cannot ride the admin path if anyone may use it: it is a local command that sends its own named message, and the host decides. | `ConsoleCommands.cs:58-80`, `ServerReceiver_Console_TryExecuteCommand` |
| `Con.RegisterCommand(Command)` exists but is internal to Together. We register through the stock `ConsoleScript.Commands` list instead, which works with and without Together. | `ConsoleCommands.cs` |
| Joins: `ScavPlayer.OnPlayerJoined` / `OnPlayerLeft`; `ScavPlayer.LocalPlayer`; `ScavPlayer.ClientIdToPlayerDict`; `ScavPlayer.BodyToPlayerDict`. | `Player.cs:167`, `458` |
| Bodies for remote players are instantiated from character prefabs in `NetBody._Internal_CreateNetBody` (`Content/NetBody.cs:1138`), so a size cannot be baked and must be applied to a live body and re-applied when a body is recreated (respawn, character change). | `NetBody.cs` |
| PvP melee runs on the host: `ServerReceiver_PlayerBodyAttack` calls `plr.body.Attack(...)`, then `DoPVPMeleeDamage(attacker, victimLimb, atk, armstrength, tool, out knockback)` (`CombatStuff.cs:361`), which multiplies by `(armstrength / armor) * PVPDamageMultiplier`. Hit validation uses `KM.DistanceCheck(..., 15)`. | `SyncSystems/CombatStuff.cs:1027-1130`, `361` |
| Carry-person exists: `SERVER_RequestCarryPerson` (`ServerMain.cs:1590`). | `ServerMain.cs` |

## Architecture

```
SizeCore/      pure C#, no Unity, no BepInEx. Formulas, rules, target-spec and command parsing, wire format.
Plugin/        the BepInEx plugin: SizeBook (who has what size), BodySizer (applies it), commands, Harmony patches, eating.
Bridge/        the only code that touches Together types. Loaded only if Together is present.
tests/         SizeCore.Tests (offline console program) and pair tests for the harness.
```

`SizeCore` compiles into the plugin and into the offline test program, the same shape as the harness `plugin-tests` in the sibling `CU TA Harness` folder.

Soft dependency: `[BepInDependency("CasualtiesMP", SoftDependency)]`. `TogetherBridge` is a separate class whose methods are called only after `Chainloader.PluginInfos.ContainsKey("CasualtiesMP")`; the JIT does not resolve Together types until such a method runs, so the plugin loads in singleplayer with no Together DLL. Together and LiteNetLib are compile references with `Private=false`.

## Size state

`SizeBook`: `Dictionary<int playerKey, float size>`; key is the Together `playerId` in multiplayer and `0` in singleplayer. Size is clamped to `[SizeMin, SizeMax]` (defaults 0.25 and 4; a request outside is refused with that message). The book on the host is authoritative; clients hold a mirror.

`BodySizer` adds a `SizedBody` component to the Body GameObject. `SizedBody`:

- records the prefab's own `heightMultiplier`, `col.size`, `col.offset`, `origColSize`, each `Limb.baseMass` and `Body.baseMass` on first sight, so Milky and Dune keep their stature and the factor multiplies on top;
- eases the current size toward the target (`SizeEaseSeconds`, default 0.4) to avoid a physics pop;
- applies the current size `s` after the game's own writes (`LateUpdate`): `heightMultiplier = base * s`, `col.size`/`col.offset` and `origColSize` times `s`, every `Limb.transform.localScale` times `s` using the game's last written value as the baseline (the weight code at `Body.cs:4157` writes a fresh `(x,1,1)`, detected by comparing with the value this component wrote last frame);
- scales `Limb.baseMass`, `Body.baseMass` and the live `rb.mass` values by `s^2` (2D mass follows area);
- toggles every limb `HingeJoint2D.enabled` when the size changed while ragdolled so Box2D rebuilds the joints at the new scale (they are rebuilt anyway when `Ragdoll()` flips `simulated`);
- counter-scales held items so a held item keeps its world size: `local = (1 / (limb.x * s), 1 / s, 1)`;
- for the local player scales `Camera.main.orthographicSize` by `s^CameraZoomExponent` (default 0.5) from the remembered base;
- overrides `overEncumberance` in a `Body.Update` postfix with `Clamp01(total / (max * s^CarryExponent) - 1)` (default exponent 2, matching mass).

**Unverified until run:** that joint rebuild by toggling is enough, that `origColSize` is the only world-space size the controller reads, camera base size is stable. Proof: `size @m 2`, `size @m 0.5` in singleplayer, then readbacks (`col` world bounds, limb world bounds, joint anchors, `baseMass`) through the link `get` command plus a screenshot, then ragdoll and stand again and re-read.

## The relational rules

All strengths are named numbers in `SizeRules` with the defaults below; the host can change any of them with `sizerule <name> <value>` and clients receive them with the snapshot. Ratio `r = size(actor) / size(other)`. A body with no partner (a fall, a bullet, an animal, a block) uses a partner of 1, the world scale.

| Rule | Formula | Default |
|---|---|---|
| Melee and dig damage by the attacker | `Clamp(size^DamageExponent, DamageMultMin, DamageMultMax)` on `atk.damage` and `atk.structuralDamage` | exponent 0.75, clamp 0.25..4 |
| Fragility of the victim | `Clamp(size^-DamageExponent, ...)` on PvP melee `armstrength` | same |
| Pair product | attacker term times victim term equals `Clamp((a/v)^DamageExponent)` when no clamp bites | tested |
| Fall and impact damage from the world | `Clamp(size^FallDamageExponent, DamageMultMin, DamageMultMax)` on `Limb.ImpactDamage` force: small bodies take less, big more | 0.5 |
| Fall gravity | `size^FallGravityExponent` on the Body and limb `gravityScale`: small bodies gain speed more slowly | 0.5 |
| Dig speed | `Clamp(size^DigExponent, DigMultMin, DigMultMax)` on `atk.structuralDamage` | 1.0, clamp 0.25..8 |
| Reach | `atk.distance * size^ReachExponent` | 0.5 |
| Weight of a carried player (Together piggyback, `NetBody.GetRealFullEncumberanceOfBody`) | times `size^CarryExponent` of the carried body, so with the carrier's own capacity above the load follows `(carried / carrier)^CarryExponent` | 2.0 |
| Carry capacity | `maxEncumberance * size^CarryExponent` | 2.0 |
| Stomp | actor foot limb moving down at `>= StompMinSpeed` relative to the other body, foot above the other's torso top, `r >= StompRatio`; force passed to `ImpactDamage` = `Min(relSpeed, StompSpeedCap) * StompForcePerRatio * r`, knockdown via `Ragdoll()`, pair cooldown `StompCooldown` | ratio 1.6, min speed 4, force 0.9 per ratio, cap 20, cooldown 0.6 s |
| Pick up | `r >= PickupRatio` | 1.5 |
| Eat | `r >= EatRatio` and rule `EatEnabled` | ratio 2.0, enabled 1 |
| Hitbox / easier to hit | geometric: colliders follow the Body scale, so the cross-section grows with size (no number) | n/a |
| Who may size whom | `SizePermission` 0 anyone, 1 self only, 2 host only | 0 |

Stomp runs where the other body is authoritative to the host: in multiplayer only the host evaluates pairs (positions are already synced to it), applies `ImpactDamage` and queues Together's health sync; in singleplayer it evaluates the player against animals (a `BuildingEntity` with `animal` set) using a size of 1 for them. It is geometry in `FixedUpdate`, not a collision callback, because puppet bodies on a client may not generate collisions. **Unverified:** whether `ImpactDamage` on a host-side remote body reaches the owning client through Together's health sync. Proof: pair test measuring the victim's `skinHealth` on both games.

Singleplayer has one player body, so pairs are player versus animals, blocks, items and the world; all rules degrade to a partner of 1.

## Console and targets

Registered in `ConsoleScript.RegisterAllCommands` (Harmony postfix, so Together or no Together):

```
size <target> <factor|reset>     set or clear a size
size <target>                    show one size
sizes                            list every size and the active rules
sizerule [name [value]]          show or set a rule (host only in multiplayer)
eat <eater> <target>             eater swallows target (rule gated)
release <target>                 let a held or swallowed body out
```

Target grammar is Together's, reused: `@m @a @o @r @r:N @c`, `id:N`, `body:N`, `team:`, a name or name prefix. In multiplayer the client sends the raw target string and the caller identity is the sender of the packet, so the host resolves `@m` and `@c` against the right player by calling `Con.GetPlayersForCommand`. In singleplayer a local resolver handles `@m @a @r @c` and `player` (all the same body). The parser, the factor grammar (`2`, `0.5`, `x2`, `150%`, `reset`) and the rule table are in `SizeCore` and tested offline.

## Multiplayer protocol

Everything goes through Together; the plugin opens no socket.

Registration, read from source:

- Messages: `Multiplayer.RegisterCustomServerReceiver(name, Action<ScavPlayer, NetDataReader>)` and `RegisterCustomClientReceiver(name, Action<NetDataReader>)` (`ScavMultiplayer.cs:2082`, `2091`), writer from `Multiplayer.CreateNamedWriter(name)`, sent with `Net.Client_Send` (client to host), `Net.Server_SendTo` (host to one) and `Net.Server_SendToClients` (host to a list). Together's own `FacePause`, `CustomCommand` use the same path. Receivers run inside try/catch in Together (`ScavMultiplayer.cs:2115-2140`) and an unknown hash is dropped.
- Commands: `Con.RegisterCommand_LocalOnly(command)` for every command, so they appear in `help`, get Together's player autofill (`Con.UpdatePlayerListAutocomplete`) and are **not** forwarded by admin mode (`localonly_commands`, `ConsoleCommands.cs:2792`). Local-only is deliberate: the command runs on the typing machine and sends its own `Request`, which is what lets anyone size anyone without being an admin. `RegisterCommandModern` is not used because it indexes every declared argument and so cannot express optional arguments (`sizes`, `sizerule`). With no Together the same `Command` objects are added to `ConsoleScript.Commands` directly.
- Entity versus message: `SyncBody` is a fixed LiteEntitySystem class that Together registers; nothing outside can add a field to it, and the size must exist before and without a body (a player who has not spawned yet, a respawn). So the size table is a custom message table: the full table is sent on `Welcome` and again on every change (a handful of ints and floats), reliable ordered, host to clients that said `Hello`.
- Late joiners: Together has no join hook that a plugin can use for a pull, but `ScavPlayer.OnPlayerJoined` is not needed: the joining client sends `Hello` once it has a local player and connection, retried every 2 s, and the host answers with `Welcome`. No Together change is required, so nothing was asked of cu-mp-robust.

Messages (all named `CasualtiesSize.<name>`):

| Name | Direction | Payload |
|---|---|---|
| `Hello` | client to host | protocol version (ushort) |
| `Welcome` | host to that client | protocol version, rules blob, size table blob |
| `Request` | client to host | verb byte, target, factor, rule name, rule value, secondary target (strings at most 64 chars) |
| `Sizes` | host to hello'd clients | size table blob (int key, float size, clamped on arrival) |
| `Rules` | host to hello'd clients | rules blob (name, float) |
| `Captures` | host to hello'd clients | count, then held key, holder key, mode |
| `Struggle` | client to host | none (sender is the held body) |
| `Reply` | host to requester | console text, at most 400 chars |

The host is authoritative: a `Request` is checked against `SizePermission`, targets resolved with `Con.GetPlayersForCommand(spec, sender, false, true)` so `@m` and `@c` refer to the sender, applied to the host `SizeBook`, then broadcast. The size table and rule table are cleared whenever the network starts or stops, so singleplayer keys never leak into a session.

Together's own mod-list check, read from source: the join packet carries the BepInEx plugin GUIDs of the player (Together and its updater removed, `ScavMultiplayer.GetBepinPlugins`). The host rejects a mismatching player with "This server requires different set of mods" **only when** `SERVER_TOGGLE_ENFORCE_MODLIST` is on (default off, toggle in the host menu; `ScavMultiplayer.cs:1259`); the server browser also greys such a lobby (`enforcemodlist_locked`, `SteamworksNet.cs:811`). Policy:

- Both sides must run the plugin for size to work. This is documented in the README.
- Default (enforcement off): a client without the plugin joins and plays normally; it is never sent a size message; it sees other bodies at normal size because the sync is positions in world space, and its own body is sized only on hosts' machines and clients that have the plugin. Nothing crashes on either side.
- Enforcement on: Together refuses the join, which is the strict form of the same policy.
- Host without the plugin: a client's `Hello` goes unanswered; `size` replies "waiting for the host to answer, or the host does not run CasualtiesSize" and changes nothing locally.
- Protocol mismatch: `Welcome` carries the host version; a different version makes the client refuse and print why.

Proofs queued for the pair rig (none run yet, see README): host sizes self and the client sees it; client sizes the host; reset; permission rule; `sizes` listing; late join by `disconnect` then `connect`; a client copy without the plugin joins, is not crashed, and is not changed. Not testable without the host menu: enforcement on (toggle is a GUI control with no launch flag).

## Pick up and eat

One state, `Capture(holder, held, mode)`, `mode` Held or Swallowed. Fits the game: a body is already a sink for items and containers, `eatTime` and `Eat()` already tie eating to hunger, and Dune's cannibal and carnivore rules already say a body is food.

- Allowed when `r >= PickupRatio` (Held) or `r >= EatRatio` and `EatEnabled` (Swallowed), the held body is not already captured, the holder is not captured.
- Held: the held body's Rigidbody is pinned to the holder's hand slot position each `FixedUpdate`, pain unchanged, can talk and look.
- Swallowed: pinned to the holder's torso, sprites sorted behind the holder's, collider disabled so it cannot be hit or collide, and each second it takes `EatDigestionDps * r` as skin and muscle loss on the torso limbs with pain, while the holder gains `EatHungerGain` hunger per point (through `Body.Eat`, so foodType rules and vomiting still apply and Milky or Dune quirks hold). A swallowed Dune is a cannibal case; the existing Dune rules are not changed.
- The captured player keeps control of their camera and can use the console, can talk, and escapes by pressing jump `EatEscapeStruggles` times (default 10, one count per press, decays 1 per second), by `release`, when `EatMaxSeconds` (90) passes, or when the holder falls unconscious or dies.
- Held and Swallowed both end by placing the held body at the holder's feet-front with a short stun.
- In multiplayer the holder's host runs the state machine; the captured player's own client applies the pin (it owns its body); `Capture` messages keep everyone's view consistent.

**Unverified:** the pin in multiplayer where the captured body is owned by another client. Proof in the pair rig: host swallows client, both games read positions and `digestion` counters.

## Not implemented (by design or deferred)

- Footstep and voice pitch by size: no single hook; needs a `Sound.Play` prefix with the body known. Deferred.
- Movement speed and jump height by size: left at the prefab values on purpose, so a giant is not faster.
- Save files do not store sizes.
- Animal sizes are 1; stomping an animal uses the stomp rule against a partner of 1.

## Debug

Everything diagnostic is off: no overlays, no per-frame logging. `CasualtiesSize.cfg` has `Debug = false`; with it on, state changes are logged to `BepInEx/LogOutput.log` and `sizes` prints extra columns.
