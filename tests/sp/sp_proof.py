import argparse
import json
import os
import shutil
import subprocess
import sys
import time

HARNESS_ROOT = os.environ.get("CU_TA_HARNESS", r"C:\Hive\CU TA Harness")
sys.path.insert(0, os.path.join(HARNESS_ROOT, "client"))

from casualtieslink.launcher import GameProcess, game_dir  # noqa: E402
from casualtieslink.lock import GameLock  # noqa: E402

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from support import FocusKeeper  # noqa: E402

PLUGIN_FOLDER = os.path.join("BepInEx", "plugins", "CasualtiesSize")
BUILT_DLL = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "bin", "CasualtiesSize.dll")
BACKUP_ROOT = os.path.join(os.environ.get("TEMP", "."), "CasualtiesSize-plugin-backup")
LINK_PORT = 7650
WORLD_TIMEOUT = 120.0
FIELDS = {
    "height": "heightMultiplier",
    "colSize": "col.size",
    "colOffset": "col.offset",
    "limb0Scale": "limbs[0].transform.localScale",
    "limb3Scale": "limbs[3].transform.localScale",
    "limb0Mass": "limbs[0].baseMass",
    "limb3Mass": "limbs[3].baseMass",
    "limb3Anchor": "limbs[3].joint.anchor",
    "limb3Connected": "limbs[3].joint.connectedAnchor",
    "bodyMass": "rb.mass",
    "standing": "standing",
    "encumbrance": "overEncumberance",
    "torsoPos": "limbs[2].transform.position",
    "headPos": "limbs[0].transform.position",
    "footPos": "limbs[8].transform.position",
}


def install_plugin(game_directory):
    target = os.path.join(game_directory, PLUGIN_FOLDER)
    if os.path.isdir(target):
        shutil.rmtree(BACKUP_ROOT, ignore_errors=True)
        shutil.copytree(target, BACKUP_ROOT)
    os.makedirs(target, exist_ok=True)
    shutil.copy2(BUILT_DLL, os.path.join(target, "CasualtiesSize.dll"))
    source = os.path.getsize(BUILT_DLL)
    installed = os.path.getsize(os.path.join(target, "CasualtiesSize.dll"))
    if source != installed:
        raise RuntimeError("installed dll size %d differs from built %d" % (installed, source))


def snapshot(game):
    row = {}
    for label, path in FIELDS.items():
        try:
            row[label] = game.get("player", path)
        except Exception as error:
            row[label] = "ERR " + str(error)[:80]
    try:
        row["camera"] = game.get("type:Camera", "orthographicSize")
    except Exception as error:
        row["camera"] = "ERR " + str(error)[:80]
    return row


def settle(game, seconds):
    deadline = time.time() + seconds
    while time.time() < deadline:
        game.step(10)
        time.sleep(0.05)


def run(arguments):
    directory = game_dir()
    with GameLock("CasualtiesSize sp_proof") as _lock, FocusKeeper():
        install_plugin(directory)
        process = GameProcess(LINK_PORT, ["--mp-disable"], manage_save=True)
        report = {}
        try:
            process.start()
            game = process.game
            game.ensure_menu()
            game.wait_state("menu")
            game.start_run(character=arguments.character)
            game.wait_state("in-world", timeout=WORLD_TIMEOUT)
            game.call("close_console")
            settle(game, 3)
            for step_name, command in (("before", None), ("x2", "size @m 2"), ("x0.5", "size @m 0.5"), ("reset", "size @m reset")):
                if command:
                    game.console(command)
                    settle(game, 3.5)
                report[step_name] = snapshot(game)
                if command:
                    report[step_name]["console"] = str(game.call("console_log"))[-300:]
            with open(arguments.output, "w", encoding="utf-8") as handle:
                json.dump(report, handle, indent=1, default=str)
            print("wrote", arguments.output)
        finally:
            process.close()


def main(argv):
    parser = argparse.ArgumentParser()
    parser.add_argument("--character", type=int, default=0)
    parser.add_argument("--output", default=os.path.join(os.environ.get("TEMP", "."), "sp_proof.json"))
    run(parser.parse_args(argv))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
