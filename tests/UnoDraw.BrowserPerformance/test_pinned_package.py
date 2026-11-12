from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch

from pinned_package import (FIXTURE_SHA256, PINS, RECEIPT, REPOSITORY, RUN_HEAD, RUN_ID,
                            canonical_digest, digest, extract_verified, inventory,
                            verify_metadata, verify_package)


class PinnedPackageTests(unittest.TestCase):
    def metadata(self, pin):
        return {"id": pin["artifact_id"], "name": "unodraw-performance-package-candidate",
                "size_in_bytes": pin["archive_bytes"], "digest": "sha256:" + pin["archive_sha256"],
                "expired": False, "workflow_run": {"id": RUN_ID, "head_sha": RUN_HEAD}}

    def fixture(self, root: Path):
        index = b"<!DOCTYPE html><title>immutable snapshot</title>"
        font = b"font identity"
        manifest = {"schema": 2, "role": "candidate", "source_sha": RUN_HEAD,
                    "index_sha256": hashlib.sha256(index).hexdigest(), "fixture_sha256": FIXTURE_SHA256,
                    "configuration": "Release", "execution_mode": "InterpreterAndAOT",
                    "runtime_generation": "uno7", "uno_sdk": "7.0.5",
                    "dotnet_sdk": "10.0.403", "dotnet_workload_version": "10.0.403",
                    "font_assets_sha256": {"font.ttf": hashlib.sha256(font).hexdigest()}}
        archive = root / "artifact.zip"
        with zipfile.ZipFile(archive, "w") as output:
            output.writestr("benchmark-package.json", json.dumps(manifest))
            output.writestr("UnoDraw.runtimeconfig.json", "{}")
            output.writestr("Uno.Fonts.OpenSans.uprimarker", "")
            output.writestr("wwwroot/index.html", index)
            output.writestr("wwwroot/font.ttf", font)
            output.writestr("wwwroot/_framework/app.wasm", b"original executable")
        pin = {**PINS["candidate"], "archive_sha256": digest(archive), "archive_bytes": archive.stat().st_size,
               "index_sha256": manifest["index_sha256"], "manifest_sha256": canonical_digest(manifest)}
        extract_verified(archive, root, pin["archive_sha256"])
        receipt = {"repository": REPOSITORY, "role": "candidate", "artifact_metadata": self.metadata(pin),
                   "archive_sha256": pin["archive_sha256"], "wwwroot_sha256": inventory(root / "wwwroot")}
        (root / RECEIPT).write_text(json.dumps(receipt))
        return pin, receipt

    def test_forged_receipt_cannot_hide_a_changed_binary(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            pin, receipt = self.fixture(root)
            with patch.dict(PINS, {"candidate": pin}):
                self.assertEqual(verify_package(root)["source_sha"], RUN_HEAD)
                (root / "wwwroot/_framework/app.wasm").write_bytes(b"substituted executable")
                receipt["wwwroot_sha256"] = inventory(root / "wwwroot")
                (root / RECEIPT).write_text(json.dumps(receipt))
                with self.assertRaisesRegex(ValueError, "asset changed"):
                    verify_package(root)

    def test_source_and_archive_identity_are_rechecked(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            pin, _ = self.fixture(root)
            with patch.dict(PINS, {"candidate": pin}):
                manifest_path = root / "benchmark-package.json"
                original = manifest_path.read_bytes()
                changed = json.loads(original)
                changed["source_sha"] = "0" * 40
                manifest_path.write_text(json.dumps(changed))
                with self.assertRaisesRegex(ValueError, "manifest differs"):
                    verify_package(root)
                manifest_path.write_bytes(original)
                with (root / "artifact.zip").open("ab") as stream:
                    stream.write(b"changed archive")
                with self.assertRaisesRegex(ValueError, "archive or provenance"):
                    verify_package(root)

    def test_wrong_run_digest_and_expired_artifacts_are_rejected(self):
        pin = PINS["candidate"]
        for mutation in (lambda m: m.update(expired=True), lambda m: m.update(digest="sha256:" + "0" * 64),
                         lambda m: m["workflow_run"].update(id=RUN_ID + 1),
                         lambda m: m["workflow_run"].update(head_sha="0" * 40)):
            metadata = self.metadata(pin)
            mutation(metadata)
            with self.assertRaises(ValueError):
                verify_metadata(metadata, "candidate")

    def test_bad_digest_and_unsafe_archive_never_extract_a_prefix(self):
        for name in ("../outside", "wwwroot/../../outside", "/absolute", "wwwroot\\outside", ".",
                     "unrecognized.json", "UnoDraw.runtimeconfig.json/nested"):
            with self.subTest(name=name), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                archive = root / "artifact.zip"
                with zipfile.ZipFile(archive, "w") as output:
                    output.writestr("wwwroot/good.txt", "must not be extracted")
                    output.writestr(name, "rejected")
                with self.assertRaisesRegex(ValueError, "SHA256"):
                    extract_verified(archive, root, "0" * 64)
                with self.assertRaisesRegex(ValueError, "unsafe entry"):
                    extract_verified(archive, root, digest(archive))
                self.assertFalse((root / "wwwroot").exists())


if __name__ == "__main__":
    unittest.main()
