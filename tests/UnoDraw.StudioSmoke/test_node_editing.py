import copy
import pathlib
import unittest
import xml.etree.ElementTree as ET

from assert_node_editing import assert_node_edit, points
from assert_svg import SVG


class NativeNodeAssertionTests(unittest.TestCase):
    def setUp(self):
        self.baseline = ET.parse(pathlib.Path(__file__).with_name("StudioFixture.svg")).getroot()

    def created(self, kind):
        root = copy.deepcopy(self.baseline)
        geometry = {"d": "M10 20L40 20L40 60L10 60"} if kind == "path" else {"points": "10,20 40,20 40,60 10,60"}
        ET.SubElement(root, SVG + kind, {"id": "node-test", "fill": "white", "stroke": "black", **geometry})
        return root

    def geometry(self, root, kind, coordinates):
        target = next(element for element in root if element.get("id") == "node-test")
        if kind == "path":
            target.set("d", "M" + "L".join(f"{x} {y}" for x, y in coordinates))
        else:
            target.set("points", " ".join(f"{x},{y}" for x, y in coordinates))

    def test_valid_saved_node_edits(self):
        for kind in ("path", "polygon", "polyline"):
            with self.subTest(kind=kind):
                created = self.created(kind)
                assert_node_edit(created, self.baseline, created, kind, "created")
                nudged = copy.deepcopy(created)
                self.geometry(nudged, kind, [(11, 30), (40, 20), (40, 60), (10, 60)])
                assert_node_edit(nudged, self.baseline, created, kind, "nudge")
                deleted = copy.deepcopy(created)
                self.geometry(deleted, kind, [(40, 20), (40, 60), (10, 60)])
                assert_node_edit(deleted, self.baseline, created, kind, "delete")

    def test_rejects_whole_object_deletion(self):
        for kind in ("path", "polygon", "polyline"):
            with self.subTest(kind=kind), self.assertRaises(AssertionError):
                assert_node_edit(self.baseline, self.baseline, self.created(kind), kind, "delete")

    def test_rejects_whole_object_translation(self):
        for kind in ("path", "polygon", "polyline"):
            created = self.created(kind)
            actual = copy.deepcopy(created)
            actual[-1].set("transform", "translate(1 10)")
            with self.subTest(kind=kind), self.assertRaises(AssertionError):
                assert_node_edit(actual, self.baseline, created, kind, "nudge")

    def test_rejects_nudging_multiple_nodes(self):
        created = self.created("polygon")
        actual = copy.deepcopy(created)
        self.geometry(actual, "polygon", [(11, 30), (41, 30), (40, 60), (10, 60)])
        with self.assertRaises(AssertionError):
            assert_node_edit(actual, self.baseline, created, "polygon", "nudge")

    def test_rejects_deleting_a_different_node(self):
        created = self.created("polyline")
        actual = copy.deepcopy(created)
        self.geometry(actual, "polyline", [(10, 20), (40, 60), (10, 60)])
        with self.assertRaises(AssertionError):
            assert_node_edit(actual, self.baseline, created, "polyline", "delete")

    def test_rejects_unrelated_artwork_change(self):
        created = self.created("path")
        actual = copy.deepcopy(created)
        self.geometry(actual, "path", [(40, 20), (40, 60), (10, 60)])
        actual[0].set("fill", "red")
        with self.assertRaises(AssertionError):
            assert_node_edit(actual, self.baseline, created, "path", "delete")

    def test_rejects_unchanged_geometry(self):
        created = self.created("polygon")
        with self.assertRaises(AssertionError):
            assert_node_edit(created, self.baseline, created, "polygon", "nudge")

    def test_rejects_nonfinite_or_malformed_points(self):
        for value in ("NaN,0 1,2", "1e999,0 1,2", "0,0 junk 1,2", "1,2 3"):
            with self.subTest(value=value), self.assertRaises(AssertionError):
                points(ET.Element(SVG + "polygon", {"points": value}))

    def test_rejects_changed_path_topology_or_empty_commands(self):
        for source in ("M11 30M40 20M40 60M10 60", "M40 20M40 60M10 60",
                       "ML11 30L40 20", "M11 30LL40 20", "M11 30L", "M11 30l40 20"):
            with self.subTest(source=source), self.assertRaises(AssertionError):
                points(ET.Element(SVG + "path", {"d": source}))


if __name__ == "__main__":
    unittest.main()
