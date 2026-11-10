import argparse
import hashlib
import os
import re
import shutil
import sys
import zipfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
DEFAULT_DLL = os.path.join(ROOT, "bin", "CasualtiesSize.dll")
DEFAULT_OUTPUT_DIRECTORY = os.path.join(os.path.expanduser("~"), "Downloads")
PLUGIN_SOURCE = os.path.join(ROOT, "src", "Plugin", "Plugin.cs")
PLUGIN_ENTRY = "BepInEx/plugins/CasualtiesSize/CasualtiesSize.dll"
INSTALL_ENTRY = "INSTALL.txt"
ZIP_TIMESTAMP = (2026, 1, 1, 0, 0, 0)

PRODUCT_NAME = "CasualtiesSize"
BEPINEX_VERSION = "5.4.23.5"
TOGETHER_NAME = "Together co-op mod"
TOGETHER_MIN_VERSION = "5.4.1"
TOGETHER_URL = ""
AUTHOR = ""
LICENSE_NAME = ""

INSTALL_TEMPLATE = """{product} {version}

Change how big players are, on your own or together with other players. Sizes change what the
body can do: small bodies fall slowly and take less fall damage, large bodies hit harder, dig
faster, carry more and weigh more when carried.

Requirements
- Casualties: Unknown with BepInEx {bepinex}
{together_requirement}

Install
1. Close the game.
2. Unzip this file into the game folder (the one that contains CasualtiesUnknown.exe),
   so that BepInEx\\plugins\\CasualtiesSize\\CasualtiesSize.dll exists.
3. Start the game.
To remove it, delete the folder BepInEx\\plugins\\CasualtiesSize.

Commands (open the console with the ` key)
  size <target> <factor>   Set a size. Examples: size @m 2    size Bob 0.5    size @a 1.5
  size <target> reset      Back to normal size.
  sizes                    List everyone who is not normal size.
  sizerule                 Show the size rules. The host can change them, for example:
                           sizerule SizePermission 2   (only the host can change sizes)
Targets: @m yourself, @a everyone, @o everyone else, @r a random player, @c the player at
your cursor, id:N, or a name.

Size limits: 0.25 to 4. Anything outside is refused with a message.
By default anyone may change anyone's size. In multiplayer every player needs this plugin.
Sizes are kept until the game is closed or everyone leaves. Using size turns achievements off until the game is restarted.

Known limitations
- Eating, picking up and stomping other players are experimental.
- Camera zoom for large and small players may not apply.
- Footstep and voice pitch do not change with size.
{credits}"""


def together_requirement():
    text = "- For multiplayer: the %s, version %s or newer, on every player" % (TOGETHER_NAME, TOGETHER_MIN_VERSION)
    if TOGETHER_URL:
        text += "\n  Download: " + TOGETHER_URL
    return text


def credits_text():
    lines = []
    if AUTHOR:
        lines.append("Made by " + AUTHOR + ".")
    if LICENSE_NAME:
        lines.append("Licence: " + LICENSE_NAME + ".")
    lines.append("Casualties: Unknown is made by its developers. BepInEx and Harmony are by their authors.")
    return "\nCredits\n" + "\n".join(lines) + "\n"


def install_text(version):
    return INSTALL_TEMPLATE.format(product=PRODUCT_NAME, version=version, bepinex=BEPINEX_VERSION,
                                   together_requirement=together_requirement(), credits=credits_text())


def read_version(plugin_source=PLUGIN_SOURCE):
    with open(plugin_source, encoding="utf-8") as handle:
        match = re.search(r'public const string Version = "([^"]+)"', handle.read())
    if not match:
        raise ValueError("no Version constant in " + plugin_source)
    return match.group(1)


def package_file_name(version):
    return "%s-%s.zip" % (PRODUCT_NAME, version)


def file_md5(path):
    with open(path, "rb") as handle:
        return hashlib.md5(handle.read()).hexdigest()


def keep_previous(output_path):
    if not os.path.isfile(output_path):
        return None
    stem, extension = os.path.splitext(output_path)
    kept = "%s.previous-%s%s" % (stem, file_md5(output_path)[:8], extension)
    if not os.path.exists(kept):
        shutil.copy2(output_path, kept)
    return kept


def build(dll_path, output_path, version):
    if not os.path.isfile(dll_path):
        raise FileNotFoundError("build first: dotnet build -c Release (missing %s)" % dll_path)
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    keep_previous(output_path)
    with open(dll_path, "rb") as handle:
        dll_bytes = handle.read()
    text = install_text(version).replace("\n", "\r\n").encode("utf-8")
    with zipfile.ZipFile(output_path, "w", zipfile.ZIP_DEFLATED) as archive:
        for entry, content in ((INSTALL_ENTRY, text), (PLUGIN_ENTRY, dll_bytes)):
            info = zipfile.ZipInfo(entry, ZIP_TIMESTAMP)
            info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(info, content)
    return output_path


def main(argv):
    parser = argparse.ArgumentParser(description="Build the CasualtiesSize release package.")
    parser.add_argument("--dll", default=DEFAULT_DLL)
    parser.add_argument("--output-directory", default=DEFAULT_OUTPUT_DIRECTORY)
    arguments = parser.parse_args(argv)
    version = read_version()
    path = build(arguments.dll, os.path.join(arguments.output_directory, package_file_name(version)), version)
    print(path)
    print("md5", file_md5(path))
    print("dll md5", file_md5(arguments.dll))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
