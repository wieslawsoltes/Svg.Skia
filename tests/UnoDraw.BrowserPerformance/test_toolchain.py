from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from toolchain import metadata, pin, pinned_configuration, validate_versions


class ToolchainTests(unittest.TestCase):
    def test_pin_preserves_the_revision_specific_uno_sdk_and_other_configuration(self) -> None:
        for uno in ("6.5.153", "7.0.5"):
            original = {"sdk": {"version": "10.0.100", "rollForward": "latestMinor", "paths": ["$host$"]},
                        "msbuild-sdks": {"Uno.Sdk": uno}, "test": {"runner": "unchanged"}}
            saved = copy.deepcopy(original)
            effective = pinned_configuration(original)
            self.assertEqual(original, saved)
            self.assertEqual(effective["msbuild-sdks"], original["msbuild-sdks"])
            self.assertEqual(effective["test"], original["test"])
            self.assertEqual(effective["sdk"]["paths"], original["sdk"]["paths"])
            self.assertEqual(effective["sdk"]["rollForward"], "disable")
            self.assertFalse(effective["sdk"]["allowPrerelease"])

    def test_newer_installed_sdk_or_loose_workload_manifests_cannot_pass(self) -> None:
        validate_versions("10.0.403", "10.0.403")
        for sdk, workload in (("10.0.404", "10.0.403"), ("10.0.403", "10.0.404"),
                              ("10.0.403", "10.0.400-manifests.a00bebb6")):
            with self.subTest(sdk=sdk, workload=workload), self.assertRaises(ValueError):
                validate_versions(sdk, workload)

    def test_manifest_retains_committed_configuration_and_exact_original_bytes(self) -> None:
        raw = b'{ "sdk": {"version": "10.0.100"}, "msbuild-sdks": {"Uno.Sdk": "6.5.153"} }\n'
        original = json.loads(raw)

        def command(arguments, **kwargs):
            if arguments == ["git", "show", "HEAD:global.json"]:
                return raw
            if arguments in (["dotnet", "--version"], ["dotnet", "workload", "--version"]):
                return "10.0.403\n"
            self.fail(f"Unexpected command: {arguments}")

        with tempfile.TemporaryDirectory() as directory, patch("toolchain.subprocess.check_output", side_effect=command):
            source = Path(directory)
            (source / "global.json").write_text(json.dumps(pinned_configuration(original)), encoding="utf-8")
            result = metadata(source)
            self.assertEqual(result["source_global_json"], original)
            self.assertEqual(result["source_global_json_sha256"], hashlib.sha256(raw).hexdigest())
            self.assertNotEqual(result["source_global_json_sha256"], result["effective_global_json_sha256"])
            self.assertTrue(result["source_configuration_overridden"])
            self.assertEqual(result["effective_global_json"]["msbuild-sdks"]["Uno.Sdk"], "6.5.153")
            unrelated = pinned_configuration(original)
            unrelated["msbuild-sdks"]["Uno.Sdk"] = "unrelated-local-edit"
            (source / "global.json").write_text(json.dumps(unrelated), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "unrelated local"):
                pin(source)
            with self.assertRaisesRegex(ValueError, "exact SDK/workload override"):
                metadata(source)


if __name__ == "__main__":
    unittest.main()
