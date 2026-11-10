import argparse
import os
import shutil
import subprocess
import sys

DEFAULT_GAME_DIR = r"C:\Program Files (x86)\Steam\steamapps\common\Casualties Unknown"
GAME_DIR_VARIABLE = "CU_GAME_DIR"
PLUGIN_FOLDER = os.path.join("BepInEx", "plugins", "CasualtiesSize")
BUILT_DLL = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "bin", "CasualtiesSize.dll")


def game_is_running():
    listing = subprocess.run(["tasklist", "/FI", "IMAGENAME eq CasualtiesUnknown.exe"], capture_output=True, text=True).stdout
    return "CasualtiesUnknown.exe" in listing


def install(game_dir, dll_path):
    if not os.path.isfile(dll_path):
        raise FileNotFoundError("build first: dotnet build -c Release (missing %s)" % dll_path)
    if game_is_running():
        raise RuntimeError("CasualtiesUnknown.exe is running; close it before installing")
    target = os.path.join(game_dir, PLUGIN_FOLDER)
    os.makedirs(target, exist_ok=True)
    shutil.copy2(dll_path, os.path.join(target, "CasualtiesSize.dll"))
    return target


def uninstall(game_dir):
    if game_is_running():
        raise RuntimeError("CasualtiesUnknown.exe is running; close it before removing")
    shutil.rmtree(os.path.join(game_dir, PLUGIN_FOLDER), ignore_errors=True)


def main(argv):
    parser = argparse.ArgumentParser(description="Install or remove the CasualtiesSize plugin.")
    parser.add_argument("--game-dir", default=os.environ.get(GAME_DIR_VARIABLE, DEFAULT_GAME_DIR))
    parser.add_argument("--dll", default=BUILT_DLL)
    parser.add_argument("--remove", action="store_true")
    arguments = parser.parse_args(argv)
    if arguments.remove:
        uninstall(arguments.game_dir)
        print("removed")
    else:
        print("installed to", install(arguments.game_dir, arguments.dll))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
