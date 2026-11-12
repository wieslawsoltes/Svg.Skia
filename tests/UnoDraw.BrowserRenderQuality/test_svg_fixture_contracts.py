from __future__ import annotations

import base64
import copy
import hashlib
import io
import json
import re
import tempfile
import textwrap
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

from PIL import Image, ImageDraw

from svg_fixture_contracts import (BRIDGES, DESKTOP, DPRS, SCENES, STATES, SVG, XLINK,
                                   anchor_colors, compare_scene, inspect_scene, load_fixtures,
                                   matrix_failures, normalize_newlines, probes,
                                   require_camera_change, require_source_readback, scene_bridges)
from svg_bridge_quality import compare_cases, load_package
from quality import raw_comparison


def xml_shape(element: ET.Element) -> tuple:
    return (element.tag, dict(element.attrib), (element.text or "").strip(),
            tuple(xml_shape(child) for child in element))


class SvgFixtureContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest, cls.fixtures = load_fixtures()
        cls.fixture = cls.fixtures[0]

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.directory = Path(self.temporary.name)
        self.addCleanup(self.temporary.cleanup)

    def synthetic(self, fixture=None, dpr=1):
        fixture = fixture or self.fixture
        scale, origin_x, origin_y = 4 * dpr, 180 * dpr, 240 * dpr
        image = Image.new("RGB", (round(1440 * dpr), round(960 * dpr)), (30, 30, 30))
        draw = ImageDraw.Draw(image)

        def rectangle(x, y, width, height, color):
            draw.rectangle((round(origin_x + x * scale), round(origin_y + y * scale),
                            round(origin_x + (x + width) * scale) - 1,
                            round(origin_y + (y + height) * scale) - 1), fill=color)

        rectangle(0, 0, 200, 140, (255, 255, 255))
        for stripe in range(8):
            rectangle(20 * stripe, 0, 20, 110, (30 + 25 * stripe, 120 + 10 * stripe, 40 + 3 * stripe))
        for y, color in zip((4, 128), anchor_colors(fixture.name)):
            rectangle(170, y, 24, 8, color)
        for probe in probes():
            rectangle(probe["x"], probe["y"], probe["width"], probe["height"], probe["expected"])
        path = self.directory / f"{fixture.name}-{dpr}.png"
        image.save(path)
        return path, inspect_scene(path, fixture, dpr)

    def changed_source(self, fixture, mutate):
        root = ET.fromstring(fixture.browser_source)
        mutate(root)
        return ET.tostring(root, encoding="unicode")

    def complete_matrix(self, candidate_bridge="webgl"):
        cases = [{"scene": scene, "bridge": bridge, "dpr": dpr, "passed": True,
                  "source_edit": {"semantic_body_verified": True, "exact_inserted_readback": True,
                                  "reopened_after_apply": True},
                  "states": [{"name": name} for name in STATES]}
                 for scene in SCENES for bridge in scene_bridges(candidate_bridge) for dpr in DPRS]
        rows = [{"scene": scene, "dpr": dpr, "state": state, "passed": True}
                for scene in SCENES for dpr in DPRS for state in STATES]
        return cases, rows

    def test_canonical_bodies_are_exact_native_fixture_bodies(self):
        root = Path(__file__).resolve().parents[2]
        native = root / self.manifest["native_fixture_source"]
        source = native.read_text(encoding="utf-8")
        self.assertEqual(hashlib.sha256(native.read_bytes()).hexdigest(), self.manifest["native_fixture_source_sha256"])
        self.assertEqual(tuple(fixture.name for fixture in self.fixtures), SCENES)
        for fixture in self.fixtures:
            with self.subTest(scene=fixture.name):
                match = re.search(r'"' + re.escape(fixture.name) + r'" => \$?"""\n(.*?)\n                """', source, re.S)
                self.assertIsNotNone(match)
                body = textwrap.dedent(match.group(1))
                if fixture.name == "image":
                    image = ET.fromstring(fixture.source).find(f"{{{SVG}}}image")
                    encoded = image.attrib[f"{{{XLINK}}}href"].split(",", 1)[1]
                    body = body.replace("{CreateEmbeddedImage()}", encoded)
                actual_body = fixture.source.split("\n", 1)[1].rsplit("\n</svg>", 1)[0]
                self.assertEqual(actual_body, body)

    def test_embedded_image_retains_native_premultiplied_png_and_alpha_pattern(self):
        fixture = next(value for value in self.fixtures if value.name == "image")
        image = ET.fromstring(fixture.source).find(f"{{{SVG}}}image")
        data = base64.b64decode(image.attrib[f"{{{XLINK}}}href"].split(",", 1)[1], validate=True)
        self.assertEqual(hashlib.sha256(data).hexdigest(), self.manifest["embedded_png_sha256"])
        with Image.open(io.BytesIO(data)) as png:
            self.assertEqual(png.size, (4, 4))
            rgba = png.convert("RGBA")
            for y in range(4):
                for x in range(4):
                    expected = (31 + 57 * x, 19 + 61 * y, 229 - 23 * x - 17 * y, 37 + 29 * x + 31 * y)
                    actual = rgba.getpixel((x, y))
                    self.assertEqual(actual[3], expected[3])
                    self.assertLessEqual(max(abs(a - b) for a, b in zip(actual[:3], expected[:3])), 4)

    def test_wrapper_preserves_body_and_keeps_calibration_outside_it(self):
        for fixture in self.fixtures:
            root = ET.fromstring(fixture.browser_source)
            canonical = ET.fromstring(fixture.source)
            self.assertEqual(tuple(xml_shape(child) for child in root[0]), tuple(xml_shape(child) for child in canonical))
            for rectangle in root[1].iter(f"{{{SVG}}}rect"):
                self.assertGreater(float(rectangle.attrib["x"]), 160)
            self.assertTrue(require_source_readback(fixture.browser_source, fixture)["semantic_body_verified"])

    def test_known_svg_serialization_forms_preserve_semantics(self):
        for fixture in self.fixtures:
            root = ET.fromstring(fixture.browser_source)
            root.set("version", "1.1")
            root.set("viewBox", "0, 0, 200, 140")
            root.set("width", "200px")
            for node in root.iter():
                style = []
                for key in ("fill", "stroke"):
                    if key in node.attrib:
                        style.append(key + ":" + node.attrib.pop(key))
                if style:
                    node.set("style", ";".join(style) + ";")
                if node.tag.endswith("}stop") and "offset" in node.attrib:
                    node.set("offset", str(float(node.get("offset")) * 100) + "%")
                if node.get("patternTransform") == "rotate(13)":
                    node.set("patternTransform", "rotate(13, 0, 0)")
            serialized = ET.tostring(root, encoding="unicode").replace("\n", "\r\n")
            self.assertTrue(require_source_readback(serialized, fixture)["semantic_body_verified"])
        self.assertEqual(normalize_newlines("a\r\nb\rc\n"), "a\nb\nc\n")

    def test_readback_rejects_stale_scene_and_matching_strip_without_body(self):
        fixture = self.fixture
        stale = self.fixtures[1].browser_source
        with self.assertRaises(AssertionError):
            require_source_readback(stale, fixture)
        mutations = [lambda root: root.remove(root[0]), lambda root: root[0].clear(),
                     lambda root: root.set("data-fixture-sha256", "0" * 64),
                     lambda root: root.append(copy.deepcopy(root[1]))]
        for mutate in mutations:
            with self.subTest(mutation=mutate), self.assertRaises(AssertionError):
                require_source_readback(self.changed_source(fixture, mutate), fixture)

    def test_readback_rejects_added_rendering_attributes_and_extra_elements(self):
        for tag, value in (("transform", "translate(2 3)"), ("opacity", ".5"), ("display", "none"),
                           ("style", "fill-opacity:.5")):
            for target in (0, 1):
                def change(root, key=tag, setting=value, position=target):
                    (root if position == 0 else root[0]).set(key, setting)
                with self.subTest(attribute=tag, target=target), self.assertRaises(AssertionError):
                    require_source_readback(self.changed_source(self.fixture, change), self.fixture)
        with self.assertRaises(AssertionError):
            require_source_readback(self.changed_source(self.fixture, lambda root: ET.SubElement(
                root, f"{{{SVG}}}rect", {"width": "200", "height": "140", "fill": "black"})), self.fixture)

    def test_readback_rejects_changed_feature_values_for_all_eight_scenes(self):
        mutations = {
            "gradients": ("stop", "stop-color", "#ff00ff"),
            "filters": ("feGaussianBlur", "stdDeviation", "3.35"),
            "mask": ("mask", "width", "150"),
            "clip": ("path", "d", "M0 0 L160 0 L160 110 Z"),
            "pattern": ("pattern", "patternTransform", "rotate(12)"),
            "text": ("text", "font-size", "26.75"),
            "image": ("image", f"{{{XLINK}}}href", "data:image/png;base64,AA=="),
            "opacity-and-strokes": ("g", "opacity", ".66"),
        }
        for fixture in self.fixtures:
            tag, key, value = mutations[fixture.name]
            def mutate(root):
                target = next(node for node in root[0].iter(f"{{{SVG}}}{tag}") if node is not root[0])
                target.set(key, value)
            with self.subTest(scene=fixture.name), self.assertRaises(AssertionError):
                require_source_readback(self.changed_source(fixture, mutate), fixture)
        fixture = next(value for value in self.fixtures if value.name == "text")
        def text_change(root):
            root[0][0].text = "Different text"
        with self.assertRaises(AssertionError):
            require_source_readback(self.changed_source(fixture, text_change), fixture)

    def test_every_scene_has_distinct_native_anchors_and_strict_color_proofs(self):
        colors = [color for scene in SCENES for color in anchor_colors(scene)]
        self.assertEqual(len(colors), len(set(colors)))
        for fixture in self.fixtures:
            for dpr in DPRS:
                with self.subTest(scene=fixture.name, dpr=dpr):
                    path, readiness = self.synthetic(fixture, dpr)
                    self.assertEqual(readiness["geometry"]["scale"], 4 * dpr)
                    self.assertEqual(len(readiness["compositing_probes"]), 8)
                    self.assertFalse(readiness["resized_for_inspection"])
                    self.assertTrue(all(probe["maximum_channel_difference"] == 0 for probe in readiness["compositing_probes"]))

    def test_blank_gray_and_calibration_only_bodies_cannot_pass(self):
        for color in ((255, 255, 255), (30, 30, 30), (128, 128, 128)):
            path, readiness = self.synthetic()
            with Image.open(path) as image:
                draw = ImageDraw.Draw(image)
                left, top, right, bottom = readiness["body_physical_bounds"]
                draw.rectangle((left, top, right - 1, bottom - 1), fill=color)
                image.save(path)
            with self.subTest(color=color), self.assertRaisesRegex(AssertionError, "body is blank/incomplete"):
                inspect_scene(path, self.fixture, 1)

    def test_previous_scene_anchors_and_css_scaled_images_cannot_pass(self):
        path, _ = self.synthetic(self.fixtures[1])
        with self.assertRaises(AssertionError):
            inspect_scene(path, self.fixture, 1)
        path, _ = self.synthetic()
        with self.assertRaisesRegex(AssertionError, "Expected native pixels"):
            inspect_scene(path, self.fixture, 2)

    def test_color_center_allows_two_levels_but_rejects_three_and_channel_swaps(self):
        path, readiness = self.synthetic()
        center = tuple(readiness["compositing_probes"][1]["center"])
        with Image.open(path) as image:
            original = image.copy()
        expected = original.getpixel(center)
        for delta, passes in ((2, True), (3, False)):
            image = original.copy()
            image.putpixel(center, (expected[0] + delta, expected[1], expected[2]))
            image.save(path)
            if passes:
                inspect_scene(path, self.fixture, 1)
            else:
                with self.assertRaisesRegex(AssertionError, "Compositing/color probe"):
                    inspect_scene(path, self.fixture, 1)
        image = original.copy()
        center = tuple(readiness["compositing_probes"][3]["center"])
        image.putpixel(center, tuple(reversed(original.getpixel(center))))
        image.save(path)
        with self.assertRaisesRegex(AssertionError, "Compositing/color probe"):
            inspect_scene(path, self.fixture, 1)

    def test_dark_matte_rejects_double_premultiplication(self):
        path, readiness = self.synthetic()
        probe = probes()[3]
        center = tuple(readiness["compositing_probes"][3]["center"])
        wrong = tuple(round(foreground * probe["opacity"] ** 2 + background * (1 - probe["opacity"]))
                      for foreground, background in zip(probe["color"], probe["background"]))
        with Image.open(path) as image:
            image.putpixel(center, wrong)
            image.save(path)
        with self.assertRaisesRegex(AssertionError, "Compositing/color probe"):
            inspect_scene(path, self.fixture, 1)

    def test_body_crop_catches_difference_diluted_by_whole_frame_and_document(self):
        before, readiness = self.synthetic()
        after = self.directory / "localized-difference.png"
        with Image.open(before) as image:
            draw = ImageDraw.Draw(image)
            # 400 pixels: below 0.1% of both whole frame and broad document,
            # above 0.1% of this fixture's actual 640×440 body.
            draw.rectangle((220, 280, 239, 299), fill=(255, 255, 255))
            image.save(after)
        raw_comparison(before, after, DESKTOP, 1)
        with self.assertRaisesRegex(AssertionError, "body pixels differ"):
            compare_scene(before, after, self.fixture, 1, readiness, readiness)

    def test_comparison_preserves_native_pixels_and_rejects_camera_mismatch(self):
        path, readiness = self.synthetic(dpr=1.25)
        result = compare_scene(path, path, self.fixture, 1.25, readiness, readiness)
        self.assertEqual(result["physical_size"], [1800, 1200])
        self.assertEqual(result["scene_body_changed_pixel_fraction"], 0)
        self.assertFalse(result["resized_for_comparison"])
        moved = copy.deepcopy(readiness)
        moved["body_physical_bounds"][0] += 2
        with self.assertRaisesRegex(AssertionError, "cameras differ"):
            compare_scene(path, path, self.fixture, 1.25, readiness, moved)

    def test_real_camera_contract_requires_fractional_pan_and_zoom(self):
        for dpr in DPRS:
            before = {"geometry": {"origin_x": 100 * dpr, "origin_y": 200 * dpr, "scale": 4 * dpr}}
            panned = {"geometry": {"origin_x": 137.25 * dpr, "origin_y": 181.5 * dpr, "scale": 4 * dpr}}
            zoomed = {"geometry": {"origin_x": 130 * dpr, "origin_y": 170 * dpr, "scale": 4.46 * dpr}}
            require_camera_change(before, panned, dpr, STATES[1])
            require_camera_change(panned, zoomed, dpr, STATES[2])
            with self.assertRaises(AssertionError):
                require_camera_change(before, before, dpr, STATES[1])
            with self.assertRaises(AssertionError):
                require_camera_change(panned, panned, dpr, STATES[2])
            zoomed["geometry"]["scale"] = 5 * dpr
            with self.assertRaises(AssertionError):
                require_camera_change(panned, zoomed, dpr, STATES[2])

    def test_complete_matrix_is_required_and_failure_cannot_be_hidden(self):
        cases, rows = self.complete_matrix()
        self.assertEqual(len(cases), 48)
        self.assertEqual(len(rows), 72)
        self.assertEqual(matrix_failures(cases, rows), [])
        mutations = [lambda c, r: c.pop(), lambda c, r: c.append(copy.deepcopy(c[0])),
                     lambda c, r: r.pop(), lambda c, r: r.append(copy.deepcopy(r[0])),
                     lambda c, r: c[0].update(passed=False), lambda c, r: r[0].update(passed=False),
                     lambda c, r: c[0]["states"].pop(),
                     lambda c, r: c[0]["states"].append(copy.deepcopy(c[0]["states"][0])),
                     lambda c, r: c[0]["source_edit"].update(semantic_body_verified=False),
                     lambda c, r: c[0]["source_edit"].update(exact_inserted_readback=False),
                     lambda c, r: c[0]["source_edit"].update(reopened_after_apply=False)]
        for mutate in mutations:
            current_cases, current_rows = copy.deepcopy(cases), copy.deepcopy(rows)
            mutate(current_cases, current_rows)
            with self.subTest(mutation=mutate):
                self.assertTrue(matrix_failures(current_cases, current_rows))

    def test_missing_cases_emit_all_72_failed_comparisons_instead_of_aborting(self):
        rows = compare_cases([], self.fixtures, self.directory)
        self.assertEqual(len(rows), 72)
        self.assertTrue(all(row["passed"] is False for row in rows))
        self.assertEqual({(row["scene"], row["dpr"], row["state"]) for row in rows},
                         {(scene, dpr, state) for scene in SCENES for dpr in DPRS for state in STATES})

    def test_graphite_uses_one_complete_48_case_72_comparison_pair_without_substitution(self):
        self.assertEqual(scene_bridges(), BRIDGES)
        self.assertEqual(scene_bridges("graphite"), ("persistent", "graphite"))
        cases, rows = self.complete_matrix("graphite")
        self.assertEqual(len(cases), 48)
        self.assertEqual(len(rows), 72)
        self.assertEqual(matrix_failures(cases, rows, candidate_bridge="graphite"), [])
        self.assertTrue(matrix_failures(cases, rows))
        for invalid in ("persistent", "composite", "legacy", "unknown"):
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                scene_bridges(invalid)
        for defect in ("substituted", "missing", "failed", "duplicate", "missing_comparison"):
            changed_cases, changed_rows = copy.deepcopy(cases), copy.deepcopy(rows)
            if defect == "substituted":
                next(case for case in changed_cases if case["bridge"] == "graphite")["bridge"] = "webgl"
            elif defect == "missing":
                changed_cases.pop()
            elif defect == "failed":
                changed_cases[0]["passed"] = False
            elif defect == "duplicate":
                changed_cases[-1] = copy.deepcopy(changed_cases[0])
            else:
                changed_rows.pop()
            with self.subTest(defect=defect):
                self.assertTrue(matrix_failures(changed_cases, changed_rows, candidate_bridge="graphite"))

    def test_missing_graphite_cases_still_emit_all_72_explicit_failures(self):
        rows = compare_cases([], self.fixtures, self.directory, candidate_bridge="graphite")
        self.assertEqual(len(rows), 72)
        self.assertTrue(all(row["passed"] is False for row in rows))

    def test_single_package_requires_explicit_mode_exact_index_and_candidate_provenance(self):
        root = self.directory / "package"
        (root / "wwwroot").mkdir(parents=True)
        index = root / "wwwroot/index.html"
        index.write_text("test publication", encoding="utf-8")
        package = {"role": "candidate", "runtime_generation": "uno7", "configuration": "Release",
                   "execution_mode": "Interpreter", "source_sha": "a" * 40, "dotnet_sdk": "10.0.403",
                   "dotnet_workload_version": "10.0.403", "index_sha256": hashlib.sha256(index.read_bytes()).hexdigest()}
        manifest = root / "benchmark-package.json"
        manifest.write_text(json.dumps(package), encoding="utf-8")
        self.assertEqual(load_package(root, "Interpreter")["source_sha"], "a" * 40)
        with self.assertRaises(ValueError):
            load_package(root, "InterpreterAndAOT")
        for key, value in (("role", "baseline"), ("source_sha", "a" * 39), ("dotnet_workload_version", ""),
                           ("configuration", "Debug"), ("index_sha256", "0" * 64)):
            manifest.write_text(json.dumps({**package, key: value}), encoding="utf-8")
            with self.subTest(key=key), self.assertRaises(ValueError):
                load_package(root, "Interpreter")


if __name__ == "__main__":
    unittest.main()
