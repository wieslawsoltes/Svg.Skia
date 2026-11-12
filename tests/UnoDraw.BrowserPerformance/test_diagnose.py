from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from diagnose import write_summary


class DiagnosticSummaryTests(unittest.TestCase):
    def test_real_phase_snapshot_and_nonempty_string_logs_are_preserved(self) -> None:
        logs = ["[frame-phases] record=187.3ms draw=4.2ms frames=60",
                "[webgpu-stats] draws=72 textureUploads=3"]
        report = {"passed": True, "backend": "webgpu", "workload": "pan",
                  "candidate_source_sha": "a" * 40,
                  "diagnostic_run": {"runtime_phase_logs": logs,
                      "managed_render_diagnostics": {"after": {"enabled": True, "phases": [
                          {"phase": "Document", "count": 120, "totalMilliseconds": 642.25,
                           "maxMilliseconds": 6.125, "allocatedBytes": 1024},
                          {"phase": "Interaction", "count": 0, "totalMilliseconds": 0,
                           "maxMilliseconds": 0, "allocatedBytes": 0}]}}}}
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            write_summary(report, output)
            text = (output / "summary.md").read_text()
        self.assertIn("| Document | 120 | 642.250 | 6.125 | 1024 |", text)
        self.assertIn("| Interaction | 0 | 0.000 | 0.000 | 0 |", text)
        for log in logs:
            self.assertIn(log, text)
        self.assertIn("not a before/after performance comparison", text)

    def test_failed_run_without_a_completed_snapshot_still_writes_its_failure(self) -> None:
        report = {"passed": False, "backend": "skia-preferred", "workload": "pan",
                  "candidate_source_sha": "a" * 40,
                  "failure": "AssertionError: The viewport did not restore",
                  "diagnostic_run": {"passed": False, "runtime_phase_logs": []}}
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            write_summary(report, output)
            text = (output / "summary.md").read_text()
        self.assertIn("Diagnostic run failed: AssertionError: The viewport did not restore", text)
        self.assertNotIn("| Document |", text)


if __name__ == "__main__":
    unittest.main()
