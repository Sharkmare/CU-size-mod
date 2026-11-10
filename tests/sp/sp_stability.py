import argparse
import json
import math
import os
import sys
import time

HARNESS_ROOT = os.environ.get("CU_TA_HARNESS", r"C:\Hive\CU TA Harness")
sys.path.insert(0, os.path.join(HARNESS_ROOT, "client"))

from casualtieslink import Game  # noqa: E402

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from support import FocusKeeper  # noqa: E402

LIMB_COUNT = 10
TORSO = 2
WALK_FRAMES = 300
SETTLE_SECONDS = 3.5
SPREAD_LIMIT_BODY_HEIGHTS = 2.2
MAX_COORDINATE = 100000.0


def limb_positions(game):
    positions = []
    for index in range(LIMB_COUNT):
        position = game.get("player", "limbs[%d].transform.position" % index)
        positions.append((position["x"], position["y"]))
    return positions


def spread(positions):
    torso = positions[TORSO]
    return max(math.hypot(x - torso[0], y - torso[1]) for x, y in positions)


def finite(positions):
    return all(math.isfinite(x) and math.isfinite(y) and abs(x) < MAX_COORDINATE and abs(y) < MAX_COORDINATE for x, y in positions)


def settle(game, seconds):
    deadline = time.time() + seconds
    while time.time() < deadline:
        game.step(10)
        time.sleep(0.05)


def walk(game, frames, key="right"):
    game.hold(action=key)
    try:
        for _ in range(frames // 10):
            game.step(10)
    finally:
        game.release(action=key)


def measure_phase(game, size):
    report = {"size": size}
    game.console("size @m %s" % size)
    settle(game, SETTLE_SECONDS)
    report["standing_spread"] = spread(limb_positions(game))
    report["camera"] = game.get("type:Camera", "orthographicSize")
    walk(game, WALK_FRAMES)
    after_walk = limb_positions(game)
    report["walk_finite"] = finite(after_walk)
    report["walk_spread"] = spread(after_walk)
    game.hold(action="ragdoll")
    settle(game, 1.0)
    report["ragdolled"] = not game.get("player", "standing")
    walk(game, WALK_FRAMES)
    ragdoll_positions = limb_positions(game)
    report["ragdoll_finite"] = finite(ragdoll_positions)
    report["ragdoll_spread"] = spread(ragdoll_positions)
    settle(game, 2.0)
    report["ragdoll_spread_after_rest"] = spread(limb_positions(game))
    game.release(action="ragdoll")
    settle(game, 4.0)
    report["restood"] = game.get("player", "standing")
    report["restood_spread"] = spread(limb_positions(game))
    return report


def main(argv):
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=7650)
    parser.add_argument("--fresh-run", action="store_true")
    parser.add_argument("--sizes", nargs="*", default=["1", "2", "0.5", "reset"])
    parser.add_argument("--output", default=os.path.join(os.environ.get("TEMP", "."), "sp_stability.json"))
    arguments = parser.parse_args(argv)
    game = Game(arguments.port)
    if arguments.fresh_run:
        game.ensure_menu()
        game.wait_state("menu")
        game.start_run(character=0)
        game.wait_state("in-world", timeout=120)
        game.call("close_console")
        settle(game, 3)
    with FocusKeeper():
        reports = [measure_phase(game, size) for size in arguments.sizes]
    with open(arguments.output, "w", encoding="utf-8") as handle:
        json.dump(reports, handle, indent=1)
    for report in reports:
        print(json.dumps(report))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
