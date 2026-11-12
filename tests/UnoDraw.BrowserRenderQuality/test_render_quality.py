from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from PIL import Image, ImageDraw

from contracts import (BACKENDS, DESKTOP, DPRS, ROLES, RULER_REGIONS, STATE_NAMES,
                       compare_states, load_packages, matrix_failures, save_ruler_strips)


def complete_case(role: str, backend: str, dpr: float) -> dict:
    return {"role": role, "backend": backend, "dpr": dpr, "passed": True,
            "states": [{"name": name, "viewport": DESKTOP, "dpr": dpr} for name in STATE_NAMES]}


class PackageContractTests(unittest.TestCase):
    def test_wrong_fixture_toolchain_mode_role_and_index_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifests = {}
            for role, sha in zip(ROLES, ("a" * 40, "b" * 40)):
                package = root / role
                (package / "wwwroot").mkdir(parents=True)
                index = b"<!doctype html><title>quality fixture</title>"
                (package / "wwwroot/index.html").write_bytes(index)
                manifests[role] = {"role": role, "source_sha": sha, "fixture_sha256": "f" * 64,
                                   "dotnet_sdk": "10.0.403", "dotnet_workload_version": "10.0.403",
                                   "configuration": "Release", "execution_mode": "InterpreterAndAOT",
                                   "runtime_generation": "uno7", "index_sha256": hashlib.sha256(index).hexdigest()}
                (package / "benchmark-package.json").write_text(json.dumps(manifests[role]), encoding="utf-8")
            self.assertEqual(set(load_packages(root)), set(ROLES))
            for key, value in (("fixture_sha256", "0" * 64), ("dotnet_sdk", "10.0.404"),
                               ("dotnet_workload_version", "10.0.404"), ("execution_mode", "Interpreter"),
                               ("configuration", "Debug"), ("runtime_generation", "uno6"),
                               ("role", "reference"), ("source_sha", "a" * 40),
                               ("source_sha", "unknown"), ("index_sha256", "0" * 64)):
                changed = {**manifests["candidate"], key: value}
                (root / "candidate/benchmark-package.json").write_text(json.dumps(changed), encoding="utf-8")
                with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                    load_packages(root)


class MatrixContractTests(unittest.TestCase):
    def test_only_all_build_backend_dpr_states_and_comparisons_can_pass(self) -> None:
        cases = [complete_case(role, backend, dpr) for role in ROLES for backend in BACKENDS for dpr in DPRS]
        rows = [{"backend": backend, "device_pixel_ratio": dpr, "state": name, "passed": True}
                for backend in BACKENDS for dpr in DPRS for name in STATE_NAMES]
        self.assertEqual(matrix_failures(cases, rows), [])
        for defect in ("missing_case", "duplicate_case", "failed_case", "wrong_backend", "missing_state",
                       "duplicate_state", "missing_comparison", "duplicate_comparison", "failed_comparison"):
            changed_cases, changed_rows = copy.deepcopy(cases), copy.deepcopy(rows)
            if defect == "missing_case":
                changed_cases.pop()
            elif defect == "duplicate_case":
                changed_cases[-1] = copy.deepcopy(changed_cases[0])
            elif defect == "failed_case":
                changed_cases[0]["passed"] = False
            elif defect == "wrong_backend":
                changed_cases[0]["backend"] = "software-fallback"
            elif defect == "missing_state":
                changed_cases[0]["states"].pop()
            elif defect == "duplicate_state":
                changed_cases[0]["states"][-1] = copy.deepcopy(changed_cases[0]["states"][0])
            elif defect == "missing_comparison":
                changed_rows.pop()
            elif defect == "duplicate_comparison":
                changed_rows[-1] = copy.deepcopy(changed_rows[0])
            else:
                changed_rows[0]["passed"] = False
            with self.subTest(defect=defect):
                self.assertTrue(matrix_failures(changed_cases, changed_rows))

    def test_wrong_backend_or_dpr_cannot_supply_a_matched_state(self) -> None:
        for key, value in (("backend", "skia-preferred"), ("dpr", 2), ("role", "baseline")):
            cases = {role: complete_case(role, "webgpu", 1.25) for role in ROLES}
            cases["candidate"][key] = value
            visited = []
            rows = compare_states(cases, "webgpu", 1.25, lambda before, after: visited.append(after))
            with self.subTest(key=key):
                self.assertEqual(visited, [])
                self.assertFalse(any(row["passed"] for row in rows))

    def test_failed_pixels_do_not_hide_later_states_or_a_missing_state(self) -> None:
        cases = {role: complete_case(role, "webgpu", 1.25) for role in ROLES}
        cases["candidate"]["states"].pop()
        visited = []

        def compare(before, after):
            visited.append(after["name"])
            if after["name"] == STATE_NAMES[0]:
                raise AssertionError("Native ruler pixels differ")
            return {"pixels": "equal"}

        rows = compare_states(cases, "webgpu", 1.25, compare)
        self.assertEqual(visited, list(STATE_NAMES[:-1]))
        self.assertEqual([row["passed"] for row in rows], [False, True, True, True, False])
        self.assertIn("Native ruler pixels differ", rows[0]["failure"])
        self.assertIn("missing from candidate", rows[-1]["failure"])


class NativeRulerArtifactTests(unittest.TestCase):
    def test_ruler_strips_copy_original_device_pixels_at_each_dpr(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            for dpr in DPRS:
                image = Image.new("RGB", (round(DESKTOP["width"] * dpr), round(DESKTOP["height"] * dpr)), "white")
                drawing = ImageDraw.Draw(image)
                for x in range(0, image.width, 2):
                    drawing.line((x, 0, x, image.height - 1), fill=(13, 57, 91))
                raw = Path(directory) / f"native-{dpr}.png"
                image.save(raw)
                strips = save_ruler_strips(raw, dpr)
                for name, bounds in RULER_REGIONS.items():
                    expected = image.crop(tuple(round(value * dpr) for value in bounds))
                    with Image.open(raw.parent / strips[name]) as actual:
                        self.assertEqual(actual.size, expected.size)
                        self.assertEqual(actual.tobytes(), expected.tobytes())

    def test_css_resolution_cannot_supply_native_ruler_artifacts(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            raw = Path(directory) / "css.png"
            Image.new("RGB", (1440, 960), "white").save(raw)
            with self.assertRaisesRegex(AssertionError, "native pixels"):
                save_ruler_strips(raw, 2)


if __name__ == "__main__":
    unittest.main()
