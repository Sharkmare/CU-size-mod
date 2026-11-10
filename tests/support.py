import subprocess
import threading

WINDOW_TITLE = "CasualtiesUnknown"
FOCUS_INTERVAL_SECONDS = 1.5
ACTIVATE_COMMAND = "(New-Object -ComObject WScript.Shell).AppActivate('%s')" % WINDOW_TITLE


def focus_game():
    subprocess.run(["powershell", "-NoProfile", "-Command", ACTIVATE_COMMAND], capture_output=True)


class FocusKeeper:
    """The game stops answering the link while its window is not in front; this keeps it there."""

    def __init__(self):
        self.stopping = threading.Event()
        self.thread = threading.Thread(target=self._loop, daemon=True)

    def _loop(self):
        while not self.stopping.wait(FOCUS_INTERVAL_SECONDS):
            focus_game()

    def __enter__(self):
        self.thread.start()
        return self

    def __exit__(self, *exc_info):
        self.stopping.set()
        self.thread.join(timeout=5)
