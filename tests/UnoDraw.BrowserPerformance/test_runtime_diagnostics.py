from __future__ import annotations

import copy
import json
import subprocess
import tempfile
import unittest
from pathlib import Path

from runtime_diagnostic_contracts import (CONFIG_PROOF_PREFIX, ENVIRONMENT, config_response,
                                          summarize_logs, verify_observation)


ORIGINAL = (b'let config = {};\nconfig.uno_app_base = "./package_immutable";\n'
            b'config.dotnet_js_filename = "dotnet.pinned.js";\n'
            b'config.environmentVariables = config.environmentVariables || {};\n'
            b'config.environmentVariables["UNCHANGED"] = "\\\\\\n\\\"literal";\nexport { config };\n')


class RuntimeConfigurationTests(unittest.TestCase):
    def test_served_module_only_appends_logging_and_preserves_the_original(self):
        served, proof = config_response(ORIGINAL)
        self.assertEqual(served[:len(ORIGINAL)], ORIGINAL)
        self.assertNotEqual(proof["original_sha256"], proof["served_sha256"])
        self.assertTrue(proof["served_package_modified"])
        self.assertFalse(proof["primary_performance_qualification"])
        with tempfile.TemporaryDirectory() as directory:
            paths = [Path(directory) / name for name in ("original.mjs", "served.mjs")]
            for path, data in zip(paths, (ORIGINAL, served)):
                path.write_bytes(data)
            command = "const a=await import(process.argv[1]);const b=await import(process.argv[2]);console.log(JSON.stringify([a.config,b.config]));"
            output = subprocess.check_output(["node", "--input-type=module", "-e", command,
                                              *(path.as_uri() for path in paths)], text=True)
            original, changed = json.loads(output.splitlines()[-1])
        self.assertEqual(changed.pop("environmentVariables"), {**original.pop("environmentVariables"), **ENVIRONMENT})
        self.assertEqual(changed, original)

    def test_unknown_duplicate_or_already_instrumented_configs_are_rejected(self):
        for content in (b"export default {};", ORIGINAL + b"export { config };\n",
                        ORIGINAL.replace(b"let config = {};", b"let config = {};\nlet config = {};"),
                        ORIGINAL.replace(b"export { config };", b'config.environmentVariables["UNO_WEBGPU_STATS"]="0";\nexport { config };'),
                        config_response(ORIGINAL)[0]):
            with self.subTest(content=content), self.assertRaises(ValueError):
                config_response(content)

    def complete_run(self):
        _, proof = config_response(ORIGINAL)
        proof.update(request_count=1, on_disk_inventory_unchanged=True)
        return {"diagnostic_pass": True, "primary_comparison": False, "configuration_override": proof,
                "metrics": {"operations": 121, "input_events": 121, "inputs_without_later_graphics": 0,
                            "inputs_without_completed_render_callback": 0, "completed_render_callbacks": 121,
                            "webgpu_canvas_submissions": 121},
                "console": [{"text": CONFIG_PROOF_PREFIX + json.dumps(ENVIRONMENT, sort_keys=True)}],
                "canonical_states": [{"name": name, "included_in_measured_window": False,
                                      "last_frame": "[webgpu-frame] cmds=1000", "last_stats": "[webgpu-stats] ops=410",
                                      "native_comparison": {"physical_size": [1440, 960], "luminance_threshold": 12,
                                                            "device_pixel_ratio": 1,
                                                            "changed_pixel_fractions": {"whole_frame": 0, "document": 0,
                                                                                        "horizontal_ruler": 0, "vertical_ruler": 0},
                                                            "maximum_changed_fraction": 0.001, "resized_for_comparison": False}}
                                     for name in ("selected", "cleared")],
                "runtime_phase_logs": ["[frame-phases] record=1ms", "[webgpu-stats] ops=410 emitted=402"]
                                      + ["[webgpu-frame] cmds=1000 renderInto=5ms opsBuild=4ms encode=1ms"] * 121}

    def test_partial_inputs_missing_runtime_proof_and_modified_assets_cannot_pass(self):
        verify_observation(self.complete_run())
        mutations = [lambda r: r.update(primary_comparison=True),
                     lambda r: r["metrics"].update(input_events=120),
                     lambda r: r["metrics"].update(inputs_without_completed_render_callback=1),
                     lambda r: r.update(console=[]), lambda r: r.update(runtime_phase_logs=[]),
                     lambda r: r.update(canonical_states=[]),
                     lambda r: r["canonical_states"][0]["native_comparison"].update(changed_pixel_fractions={}),
                     lambda r: r["canonical_states"][0]["native_comparison"]["changed_pixel_fractions"].update(whole_frame=0.002),
                     lambda r: r["canonical_states"][0]["native_comparison"]["changed_pixel_fractions"].update(document=float("nan")),
                     lambda r: r["configuration_override"].update(on_disk_inventory_unchanged=False),
                     lambda r: r["configuration_override"].update(request_count=0)]
        for mutate in mutations:
            run = copy.deepcopy(self.complete_run())
            mutate(run)
            with self.assertRaises(ValueError):
                verify_observation(run)

    def test_one_frame_stats_do_not_mix_in_sixty_frame_phase_averages(self):
        summary = summarize_logs(["[frame-phases] record=20ms draw=8ms gap=900ms",
                                  "[webgpu-stats] 1440x960: ops=426 emitted=418 scissorChanges=398 prepass=71",
                                  "[webgpu-frame] replays=1 damage=1434x919 cmds=1255 renderInto=6.8ms finishSubmit=0.4ms opsBuild=5.7ms (walk=5.5 rebuild=3.5) encode=0.8ms"])
        self.assertEqual(summary["fields"]["ops"]["median"], 426)
        self.assertEqual(summary["fields"]["opsBuild"]["median"], 5.7)
        self.assertNotIn("gap", summary["fields"])


if __name__ == "__main__":
    unittest.main()
