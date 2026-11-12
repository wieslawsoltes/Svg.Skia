"""Input automation waits for new native focus/text observations before advancing."""
from pathlib import Path
import tempfile
import threading
import time
import unittest

from wait_input import wait_for_input


class InputReadinessTests(unittest.TestCase):
    def test_an_old_matching_event_cannot_satisfy_a_new_checkpoint(self):
        with tempfile.TemporaryDirectory() as directory:
            log = Path(directory) / "application.log"
            log.write_text("[EditorInput 1 10] query-focus success=True\n")
            offset = log.stat().st_size
            with self.assertRaises(TimeoutError):
                wait_for_input(log, offset, "query-focus success=True", timeout=0.04)
            with log.open("a") as stream:
                stream.write("[EditorInput 2 20] query-focus success=True\n")
            wait_for_input(log, offset, "query-focus success=True")

    def test_a_partial_log_line_is_not_mistaken_for_a_completed_focus_event(self):
        with tempfile.TemporaryDirectory() as directory:
            log = Path(directory) / "application.log"
            log.write_text("[EditorInput 1 10] focus=StudioToolButton target=StudioReferencePoint0")
            with self.assertRaises(TimeoutError):
                wait_for_input(log, 0, "target=StudioReferencePoint0", timeout=0.04)
            with log.open("a") as stream:
                stream.write("\n")
            wait_for_input(log, 0, "target=StudioReferencePoint0")

    def test_wait_observes_an_asynchronous_complete_event(self):
        with tempfile.TemporaryDirectory() as directory:
            log = Path(directory) / "application.log"
            log.touch()

            def complete():
                time.sleep(0.04)
                with log.open("a") as stream:
                    stream.write("[EditorInput 1 10] query-results length=6 count=1 selected=save\n")

            writer = threading.Thread(target=complete)
            writer.start()
            try:
                wait_for_input(log, 0, r"query-results length=6 .*selected=save", timeout=1)
            finally:
                writer.join()


if __name__ == "__main__":
    unittest.main()
