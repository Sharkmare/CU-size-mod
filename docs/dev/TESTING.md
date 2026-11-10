# Testing

## Offline

No game needed.

```bash
cd tests/CoreTests && dotnet run -c Release
python tests/package/test_package.py
```

`CoreTests` covers the relational formulas for every size pair, rule bounds and permissions, command parsing, the wire format (including hostile payloads) and the hold timing. The package test checks that the release zip contains exactly the plugin and the install text, that the unzipped dll equals the build, that no internal wording ships, and that a rebuild keeps the previous zip.

## Two game instances

These start a host and a client game and need an idle machine: no `CasualtiesUnknown.exe` running and no `.game.lock` in the test harness folder.

The tests use the test harness (a sibling folder named `CU TA Harness`; set `CU_TA_HARNESS` to use another). Install the plugin first with `python tools/install.py`, then:

```bash
python tests/pair/test_size_pair.py ReplicationTests
python tests/pair/test_size_pair.py LateJoinTests
python tests/pair/test_size_pair.py PluginlessClientTests
```

Each class starts its own pair (about 90 seconds). The Together dll md5 is printed at the start of every class; set `CU_TOGETHER_MD5` to skip the class when the installed Together build is a different one. The harness copies the installed game folder for the client, so the plugin is on both sides; `PluginlessClientTests` removes it from the copy.

## Singleplayer

```bash
python tests/sp/sp_proof.py        # launches the game, reads geometry at sizes 2, 0.5 and reset
python tests/sp/sp_stability.py    # attaches to a running game with the link, walks and ragdolls at each size
```

The game stops answering the link when its window loses focus; both scripts keep it in front.
