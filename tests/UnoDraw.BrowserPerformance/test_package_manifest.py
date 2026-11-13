from __future__ import annotations

import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from package_manifest import main


class PackageManifestTests(unittest.TestCase):
    def test_interpreter_package_is_explicitly_labeled_and_aot_remains_the_default(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source, package = root / "source", root / "package"
            fixture = source / "samples/UnoDraw/Assets/Welcome.svg"
            fixture.parent.mkdir(parents=True)
            fixture.write_text('<svg xmlns="http://www.w3.org/2000/svg"/>', encoding="utf-8")
            (package / "wwwroot").mkdir(parents=True)
            (package / "wwwroot/index.html").write_text("<!doctype html>", encoding="utf-8")
            arguments = ["package_manifest.py", "--source", str(source), "--package", str(package),
                         "--role", "candidate"]
            metadata = {"dotnet_sdk": "10.0.403", "dotnet_workload_version": "10.0.403",
                        "source_global_json": {"msbuild-sdks": {"Uno.Sdk": "7.0.5"}}}
            for extra, expected in (([], "InterpreterAndAOT"), (["--execution-mode", "Interpreter"], "Interpreter")):
                with self.subTest(mode=expected), patch("sys.argv", arguments + extra), \
                        patch("package_manifest.metadata", return_value=metadata), \
                        patch("package_manifest.subprocess.check_output", return_value="recorded-value\n"), \
                        contextlib.redirect_stdout(io.StringIO()):
                    main()
                result = json.loads((package / "benchmark-package.json").read_text(encoding="utf-8"))
                self.assertEqual(result["execution_mode"], expected)
                self.assertEqual(result["configuration"], "Release")
                self.assertEqual(result["dotnet_workload_version"], "10.0.403")


if __name__ == "__main__":
    unittest.main()
