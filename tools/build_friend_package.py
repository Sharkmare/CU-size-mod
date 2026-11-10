import argparse
import hashlib
import os
import re
import sys
import zipfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
DEFAULT_DLL = os.path.join(ROOT, "bin", "CasualtiesSize.dll")
DEFAULT_OUTPUT = os.path.join(os.path.expanduser("~"), "Downloads", "CasualtiesSize-friend.zip")
PLUGIN_SOURCE = os.path.join(ROOT, "src", "Plugin", "Plugin.cs")
PLUGIN_ENTRY = "BepInEx/plugins/CasualtiesSize/CasualtiesSize.dll"
INSTALL_ENTRY = "INSTALL.txt"
TOGETHER_REQUIREMENT = "exactly the Together 5.4.1-dune.2 package (CasualtiesTogether-dune.zip), on every player"
ZIP_TIMESTAMP = (2026, 1, 1, 0, 0, 0)

INSTALL_TEMPLATE = """CasualtiesSize {version}

Lets players change body size in the console, in singleplayer and with the Together mod.

Needs
- Casualties: Unknown with BepInEx 5.4.23.5
- For multiplayer: {together}
- Every player in the session must have this plugin. A player without it sees no size change.

Install
Unzip this file into the game folder (the one with CasualtiesUnknown.exe), so that
BepInEx\\plugins\\CasualtiesSize\\CasualtiesSize.dll exists. Close the game first.
To remove it, delete the folder BepInEx\\plugins\\CasualtiesSize.

Commands (game console, key `)
  size <target> <factor>   set a size, for example: size @m 2   size Bob 0.5   size @a 1.5
  size <target> reset      back to normal
  sizes                    list everyone who is not normal size
Targets: @m you, @a everyone, @o everyone else, @r random, @c player at your cursor, id:N, or a name.
Limits: 0.25 to 4. Anything else is refused with a message.
Anyone may size anyone by default. The host can restrict it: sizerule SizePermission 1 (self only) or 2 (host only).

Notes
- Using size turns achievements off until you restart the game.
- Small bodies fall with less gravity and take less fall damage; a piggybacked player weighs less the smaller they are. These and the camera zoom are new and were only tested offline.
- Not tested at all, so avoid in a real session: eat, pickup, release and stomping on other players.
- A size is kept while the session runs and is gone when everyone leaves.
"""


def read_version(plugin_source=PLUGIN_SOURCE):
    with open(plugin_source, encoding="utf-8") as handle:
        match = re.search(r'public const string Version = "([^"]+)"', handle.read())
    if not match:
        raise ValueError("no Version constant in " + plugin_source)
    return match.group(1)


def file_md5(path):
    with open(path, "rb") as handle:
        return hashlib.md5(handle.read()).hexdigest()


def build(dll_path, output_path, version):
    if not os.path.isfile(dll_path):
        raise FileNotFoundError("build first: dotnet build -c Release (missing %s)" % dll_path)
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    with zipfile.ZipFile(output_path, "w", zipfile.ZIP_DEFLATED) as archive:
        for entry, content in ((INSTALL_ENTRY, INSTALL_TEMPLATE.format(version=version, together=TOGETHER_REQUIREMENT).replace("\n", "\r\n").encode("utf-8")),
                               (PLUGIN_ENTRY, open(dll_path, "rb").read())):
            info = zipfile.ZipInfo(entry, ZIP_TIMESTAMP)
            info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(info, content)
    return output_path


def main(argv):
    parser = argparse.ArgumentParser(description="Build the CasualtiesSize friend package.")
    parser.add_argument("--dll", default=DEFAULT_DLL)
    parser.add_argument("--output", default=DEFAULT_OUTPUT)
    arguments = parser.parse_args(argv)
    path = build(arguments.dll, arguments.output, read_version())
    print(path)
    print("md5", file_md5(path))
    print("dll md5", file_md5(arguments.dll))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
