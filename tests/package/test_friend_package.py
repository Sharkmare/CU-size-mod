import hashlib
import os
import sys
import tempfile
import unittest
import zipfile

TOOLS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools")
sys.path.insert(0, TOOLS)

import build_friend_package as package  # noqa: E402

EXPECTED_ENTRIES = {package.PLUGIN_ENTRY, package.INSTALL_ENTRY}
BANNED_NAME_PARTS = (".pdb", ".log", ".cfg", "debug", "tests", ".cs")


class FriendPackageTests(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        cls.workdir = tempfile.TemporaryDirectory()
        cls.dll = os.path.join(cls.workdir.name, "CasualtiesSize.dll")
        with open(cls.dll, "wb") as handle:
            handle.write(os.urandom(2048))
        cls.zip_path = os.path.join(cls.workdir.name, "out", "friend.zip")
        package.build(cls.dll, cls.zip_path, "9.9.9")

    @classmethod
    def tearDownClass(cls):
        cls.workdir.cleanup()

    def test_contains_exactly_the_plugin_and_the_install_text(self):
        with zipfile.ZipFile(self.zip_path) as archive:
            self.assertEqual(EXPECTED_ENTRIES, set(archive.namelist()))

    def test_unzipped_dll_equals_the_build(self):
        target = os.path.join(self.workdir.name, "game")
        with zipfile.ZipFile(self.zip_path) as archive:
            archive.extractall(target)
        installed = os.path.join(target, *package.PLUGIN_ENTRY.split("/"))
        self.assertTrue(os.path.isfile(installed))
        self.assertEqual(package.file_md5(self.dll), package.file_md5(installed))

    def test_nothing_debug_or_source_is_shipped(self):
        with zipfile.ZipFile(self.zip_path) as archive:
            for name in archive.namelist():
                for part in BANNED_NAME_PARTS:
                    self.assertNotIn(part, name.lower().replace("casualtiessize.dll", ""), name)

    def test_install_text_names_requirements_commands_limits_and_warnings(self):
        with zipfile.ZipFile(self.zip_path) as archive:
            text = archive.read(package.INSTALL_ENTRY).decode("utf-8")
        for needle in (package.TOGETHER_REQUIREMENT, "5.4.23.5", "size <target> <factor>", "0.25 to 4", "achievements", "Not tested", "9.9.9"):
            self.assertIn(needle, text)

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
        self.assertTrue(os.path.isfile(kept))
        self.assertEqual(first_md5, package.file_md5(kept))
        self.assertNotEqual(first_md5, package.file_md5(target))

    def test_missing_dll_is_refused(self):
        with self.assertRaises(FileNotFoundError):
            package.build(os.path.join(self.workdir.name, "none.dll"), os.path.join(self.workdir.name, "x.zip"), "1")

    def test_version_is_read_from_the_plugin_source(self):
        self.assertRegex(package.read_version(), r"^\d+\.\d+\.\d+$")


if __name__ == "__main__":
    unittest.main()
