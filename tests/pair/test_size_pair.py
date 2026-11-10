import hashlib
import math
import os
import shutil
import sys
import time
import unittest

HARNESS_ROOT = os.environ.get("CU_TA_HARNESS", r"C:\Hive\CU TA Harness")
sys.path.insert(0, os.path.join(HARNESS_ROOT, "client"))
os.environ.setdefault("CASUALTIES_PAIR_TESTS", "1")

from casualtieslink import testcopy  # noqa: E402
from casualtieslink.launcher import game_dir  # noqa: E402
from tests.test_pair import PairCase, read_bodies, wait_for_bodies  # noqa: E402

PLUGIN_FOLDER = os.path.join("BepInEx", "plugins", "CasualtiesSize")
PLUGIN_LOG_MARKER = "CasualtiesSize 0.1.0 loaded"
HOST_BODY = "Body_HostD"
CLIENT_BODY = "Body_ClientD"
REPLICATION_SECONDS = 6.0
EASE_SECONDS = 2.0
SIZE_TOLERANCE = 0.03
MASS_FIELD = "limbs[0].baseMass"
HEIGHT_FIELD = "heightMultiplier"
LIMB_SCALE_FIELD = "limbs[0].transform.localScale.x"


TOGETHER_DLL = os.path.join("BepInEx", "plugins", "Together", "CasualtiesMP.dll")
EXPECTED_TOGETHER_MD5 = os.environ.get("CU_TOGETHER_MD5", "")
LIMB_COUNT = 10
TORSO = 2
STABLE_SPREAD_LOW = 1.4
STABLE_SPREAD_HIGH = 2.8
MAX_COORDINATE = 100000.0


def together_md5():
    with open(os.path.join(game_dir(), TOGETHER_DLL), "rb") as handle:
        return hashlib.md5(handle.read()).hexdigest()


def limb_spread(game, name):
    handle = body_handle(game, name)
    points = []
    for index in range(LIMB_COUNT):
        position = game.get(handle, "limbs[%d].transform.position" % index)
        points.append((position["x"], position["y"]))
    torso = points[TORSO]
    finite = all(math.isfinite(x) and math.isfinite(y) and abs(x) < MAX_COORDINATE and abs(y) < MAX_COORDINATE for x, y in points)
    return finite, max(math.hypot(x - torso[0], y - torso[1]) for x, y in points)


def body_handle(game, name):
    rows = read_bodies(game)
    if name not in rows:
        raise AssertionError("%s not found among %s" % (name, sorted(rows)))
    return rows[name]["handle"]


def geometry(game, name):
    handle = body_handle(game, name)
    return {
        "height": float(game.get(handle, HEIGHT_FIELD)),
        "mass": float(game.get(handle, MASS_FIELD)),
        "limb": float(game.get(handle, LIMB_SCALE_FIELD)),
    }


def console_text(game):
    return str(game.call("console_log"))


def wait_until(check, timeout=REPLICATION_SECONDS, step=0.5):
    deadline = time.time() + timeout
    last = None
    while time.time() < deadline:
        last = check()
        if last:
            return last
        time.sleep(step)
    return last


def installed_plugin_folder():
    return os.path.join(game_dir(), PLUGIN_FOLDER)


class SizePairCase(PairCase):
    setchar = 0
    prefab = {}

    @classmethod
    def setUpClass(cls):
        if not os.path.isdir(installed_plugin_folder()):
            raise unittest.SkipTest("CasualtiesSize is not installed in " + installed_plugin_folder())
        actual = together_md5()
        print("Together CasualtiesMP.dll md5 %s" % actual)
        if EXPECTED_TOGETHER_MD5 and not actual.startswith(EXPECTED_TOGETHER_MD5):
            raise unittest.SkipTest("Together md5 %s is not the expected %s" % (actual, EXPECTED_TOGETHER_MD5))
        super().setUpClass()
        cls.prefab = {
            "host": geometry(cls.host, HOST_BODY),
            "client": geometry(cls.client, CLIENT_BODY),
        }

    def setUp(self):
        self.host.console("sizerule SizePermission 0")
        self.host.console("size @a reset")
        for name in (HOST_BODY, CLIENT_BODY):
            self.assertIsNotNone(self.size_of_everywhere(name, 1.0), "could not return " + name + " to size 1 before the test")

    def size_of_everywhere(self, name, expected):
        def converged():
            try:
                rows = [geometry(game, name) for game in (self.host, self.client)]
            except Exception:
                return None
            return rows if all(abs(row["height"] - self.prefab_for(name)["height"] * expected) < SIZE_TOLERANCE * expected for row in rows) else None
        return wait_until(converged, REPLICATION_SECONDS + EASE_SECONDS)

    def prefab_for(self, name):
        return self.prefab["host"] if name == HOST_BODY else self.prefab["client"]


class ReplicationTests(SizePairCase):

    def test_host_sizes_self_and_the_client_sees_it(self):
        self.host.console("size @m 2")
        rows = self.size_of_everywhere(HOST_BODY, 2.0)
        self.assertIsNotNone(rows, "host body never reached height x2 on both games")
        for row in rows:
            self.assertAlmostEqual(row["mass"], self.prefab["host"]["mass"] * 4.0, delta=self.prefab["host"]["mass"] * 0.2)
            self.assertAlmostEqual(row["limb"], self.prefab["host"]["limb"] * 2.0, delta=0.1)

    def test_sized_ragdoll_stays_stable_on_both_games(self):
        _, base = limb_spread(self.host, HOST_BODY)
        self.host.console("size @m 2")
        self.assertIsNotNone(self.size_of_everywhere(HOST_BODY, 2.0))
        self.host.hold(action="ragdoll")
        try:
            for _ in range(30):
                self.host.step(10)
            time.sleep(2.0)
            for label, game in (("host", self.host), ("client", self.client)):
                finite, spread = limb_spread(game, HOST_BODY)
                self.assertTrue(finite, label + " limbs left the world")
                self.assertTrue(STABLE_SPREAD_LOW * base < spread < STABLE_SPREAD_HIGH * base, "%s ragdoll spread %.2f vs prefab %.2f" % (label, spread, base))
        finally:
            self.host.release(action="ragdoll")
        self.host.console("size @m reset")

    def test_out_of_range_is_refused_with_a_message(self):
        self.host.console("size @m 50")
        refused = wait_until(lambda: "size must be between" in console_text(self.host))
        self.assertTrue(refused, console_text(self.host)[-300:])
        self.assertAlmostEqual(geometry(self.host, HOST_BODY)["height"], self.prefab["host"]["height"], delta=SIZE_TOLERANCE)

    def test_client_sizes_the_host_because_anyone_may(self):
        self.client.console("size HostD 0.5")
        rows = self.size_of_everywhere(HOST_BODY, 0.5)
        self.assertIsNotNone(rows, "client request did not shrink the host on both games")

    def test_reset_returns_to_the_prefab(self):
        self.client.console("size @m 3")
        self.assertIsNotNone(self.size_of_everywhere(CLIENT_BODY, 3.0))
        self.client.console("size @m reset")
        self.assertIsNotNone(self.size_of_everywhere(CLIENT_BODY, 1.0), "reset did not restore the prefab geometry")

    def test_host_permission_rule_blocks_other_players(self):
        self.host.console("sizerule SizePermission 2")
        time.sleep(1.5)
        self.client.console("size HostD 4")
        refused = wait_until(lambda: "not allowed" in console_text(self.client))
        self.assertTrue(refused, "client got no refusal: " + console_text(self.client)[-300:])
        host_now = geometry(self.host, HOST_BODY)
        self.assertAlmostEqual(host_now["height"], self.prefab["host"]["height"], delta=SIZE_TOLERANCE)
        self.host.console("sizerule SizePermission 0")

    def test_sizes_lists_both_after_two_changes(self):
        self.host.console("size @m 2")
        self.host.console("size ClientD 0.5")
        self.size_of_everywhere(CLIENT_BODY, 0.5)
        self.client.console("sizes")
        listed = wait_until(lambda: "HostD" in console_text(self.client) and "ClientD" in console_text(self.client))
        self.assertTrue(listed, console_text(self.client)[-400:])
        self.host.console("size @a reset")


class LateJoinTests(SizePairCase):

    def test_client_that_reconnects_receives_the_current_sizes(self):
        self.client.console("disconnect")
        time.sleep(3.0)
        self.host.console("size @m 2.5")
        time.sleep(1.0)
        self.client.console("connect " + "127.0.0.1:7790")
        wait_for_bodies(self.client, 2)
        rows = self.size_of_everywhere(HOST_BODY, 2.5)
        self.assertIsNotNone(rows, "reconnected client did not receive the host size")


class PluginlessClientTests(SizePairCase):

    @classmethod
    def setUpClass(cls):
        real_make_copy = testcopy.make_copy

        def make_copy_without_plugin(*args, **kwargs):
            copy = real_make_copy(*args, **kwargs)
            shutil.rmtree(os.path.join(copy, PLUGIN_FOLDER), ignore_errors=True)
            return copy

        testcopy.make_copy = make_copy_without_plugin
        cls._real_make_copy = real_make_copy
        try:
            super().setUpClass()
        finally:
            testcopy.make_copy = real_make_copy

    def test_client_without_the_plugin_neither_crashes_nor_changes(self):
        self.host.console("size ClientD 3")
        time.sleep(REPLICATION_SECONDS)
        self.assertAlmostEqual(geometry(self.client, CLIENT_BODY)["height"], self.prefab["client"]["height"], delta=SIZE_TOLERANCE)
        self.assertTrue(self.host.transport.is_up(), "host stopped answering")
        self.assertTrue(self.client.transport.is_up(), "client stopped answering")
        self.host.console("size @m 2")
        grown = wait_until(lambda: abs(geometry(self.host, HOST_BODY)["height"] - self.prefab["host"]["height"] * 2.0) < SIZE_TOLERANCE * 2.0, REPLICATION_SECONDS + EASE_SECONDS)
        self.assertTrue(grown, "host stopped applying its own sizes after a plugin-less client was sized")
        self.assertEqual(self.client.call("state"), "in-world")

    def test_client_without_the_plugin_has_no_size_command(self):
        self.client.console("size @m 2")
        self.assertIn("Invalid command", console_text(self.client))


if __name__ == "__main__":
    unittest.main()
