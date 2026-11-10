import os
import sys
import tempfile
import unittest
import zipfile

TOOLS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools")
sys.path.insert(0, TOOLS)

import build_package as package  # noqa: E402

EXPECTED_ENTRIES = {package.PLUGIN_ENTRY, package.INSTALL_ENTRY}
BANNED_NAME_PARTS = (".pdb", ".log", ".cfg", "debug", "tests", ".cs")
BANNED_WORDS = ("friend", "operator", "harness", "session", "dune", "proven", "untested", "github")
DLL_FILE_NAME = "casualtiessize.dll"


def shipped_text(archive):
    return archive.read(package.INSTALL_ENTRY).decode("utf-8")


class PackageTests(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        cls.workdir = tempfile.TemporaryDirectory()
        cls.dll = os.path.join(cls.workdir.name, "CasualtiesSize.dll")
        with open(cls.dll, "wb") as handle:
            handle.write(os.urandom(2048))
        cls.zip_path = os.path.join(cls.workdir.name, "out", package.package_file_name("9.9.9"))
        package.build(cls.dll, cls.zip_path, "9.9.9")

    @classmethod
    def tearDownClass(cls):
        cls.workdir.cleanup()

    def test_file_name_is_the_product_and_version(self):
        self.assertEqual("CasualtiesSize-9.9.9.zip", os.path.basename(self.zip_path))

    def test_contains_exactly_the_plugin_and_the_install_text(self):
        with zipfile.ZipFile(self.zip_path) as archive:
            self.assertEqual(EXPECTED_ENTRIES, set(archive.namelist()))

    def test_unzipped_dll_equals_the_build(self):
        target = os.path.join(self.workdir.name, "game")
        with zipfile.ZipFile(self.zip_path) as archive:
            archive.extractall(target)
        installed = os.path.join(target, *package.PLUGIN_ENTRY.split("/"))
        self.assertEqual(package.file_md5(self.dll), package.file_md5(installed))

    def test_nothing_debug_or_source_is_shipped(self):
        with zipfile.ZipFile(self.zip_path) as archive:
            for name in archive.namelist():
                for part in BANNED_NAME_PARTS:
                    self.assertNotIn(part, name.lower().replace(DLL_FILE_NAME, ""), name)

    def test_no_internal_wording_in_any_shipped_text_or_name(self):
        with zipfile.ZipFile(self.zip_path) as archive:
            text = shipped_text(archive).lower()
            names = " ".join(archive.namelist()).lower()
        for word in BANNED_WORDS:
            self.assertNotIn(word, text, word)
            self.assertNotIn(word, names, word)

    def test_version_and_requirement_lines_are_present(self):
        with zipfile.ZipFile(self.zip_path) as archive:
            text = shipped_text(archive)
        self.assertIn("CasualtiesSize 9.9.9", text)
        self.assertIn("BepInEx " + package.BEPINEX_VERSION, text)
        self.assertIn("%s, version %s or newer" % (package.TOGETHER_NAME, package.TOGETHER_MIN_VERSION), text)
        for needle in ("size <target> <factor>", "0.25 to 4", "achievements", "Known limitations", "Credits"):
            self.assertIn(needle, text)

    def test_no_download_link_while_the_url_constant_is_empty(self):
        self.assertEqual("", package.TOGETHER_URL)
        with zipfile.ZipFile(self.zip_path) as archive:
            text = shipped_text(archive)
        self.assertNotIn("http", text)
        self.assertNotIn("Download:", text)

    def test_link_appears_only_when_the_constant_is_set(self):
        original = package.TOGETHER_URL
        package.TOGETHER_URL = "https://example.invalid/together"
        try:
            self.assertIn("Download: https://example.invalid/together", package.install_text("1.0.0"))
        finally:
            package.TOGETHER_URL = original
        self.assertNotIn("Download:", package.install_text("1.0.0"))

    def test_author_and_licence_lines_are_omitted_until_set(self):
        text = package.install_text("1.0.0")
        self.assertNotIn("Made by", text)
        self.assertNotIn("Licence:", text)
        package.AUTHOR, package.LICENSE_NAME = "Someone", "MIT"
        try:
            filled = package.install_text("1.0.0")
        finally:
            package.AUTHOR, package.LICENSE_NAME = "", ""
        self.assertIn("Made by Someone.", filled)
        self.assertIn("Licence: MIT.", filled)

    def test_build_is_reproducible(self):
        second = os.path.join(self.workdir.name, "again.zip")
        package.build(self.dll, second, "9.9.9")
        self.assertEqual(package.file_md5(self.zip_path), package.file_md5(second))

    def test_rebuilding_over_a_zip_keeps_the_previous_one(self):
        target = os.path.join(self.workdir.name, "keep", "pkg.zip")
        package.build(self.dll, target, "1.0.0")
        first_md5 = package.file_md5(target)
        package.build(self.dll, target, "2.0.0")
        kept = os.path.join(os.path.dirname(target), "pkg.previous-%s.zip" % first_md5[:8])
        self.assertEqual(first_md5, package.file_md5(kept))
        self.assertNotEqual(first_md5, package.file_md5(target))

    def test_missing_dll_is_refused(self):
        with self.assertRaises(FileNotFoundError):
            package.build(os.path.join(self.workdir.name, "none.dll"), os.path.join(self.workdir.name, "x.zip"), "1")

    def test_version_is_read_from_the_plugin_source(self):
        self.assertRegex(package.read_version(), r"^\d+\.\d+\.\d+$")


if __name__ == "__main__":
    unittest.main()
