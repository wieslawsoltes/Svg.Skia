import unittest

from assert_navigator import verify_geometry, verify_navigation


def update(count=7, identity=2, visible=True):
    return (f"[EditorInput 1 999] navigator-viewport-update id={identity} overviewFrames={count} visible={visible}"
            " camera=-10,-5,2 viewport=100,50 overview=216,116 artboards=0,0,200,100 indicator=12.25,9.75,51.5,26.5\n")


class NavigatorAssertionTests(unittest.TestCase):
    def test_camera_updates_reuse_rendered_overview(self):
        result = verify_navigation(update() * 3)
        self.assertTrue(result["passed"])
        self.assertEqual(3, result["camera_updates"])

    def test_overview_repaint_between_camera_updates_fails(self):
        with self.assertRaises(AssertionError):
            verify_navigation(update() + "navigator-overview-render id=2 count=8\n" + update(8) * 2)

    def test_deferred_overview_repaint_after_final_update_fails(self):
        with self.assertRaises(AssertionError):
            verify_navigation(update() * 3 + "navigator-overview-render id=2 count=8\n")

    def test_other_navigator_renders_do_not_alias_the_observed_control(self):
        self.assertTrue(verify_navigation(update() + "navigator-overview-render id=5 count=10\n" + update() * 2)["passed"])

    def test_missing_unrendered_or_hidden_overlay_fails(self):
        for text in (update() * 2, update(0) * 3, update() * 2 + update(visible=False),
                     update(identity=1) + update(identity=2) * 2):
            with self.subTest(text=text), self.assertRaises(AssertionError):
                verify_navigation(text)

    def test_stale_indicator_after_pan_fails(self):
        with self.assertRaises(AssertionError):
            verify_navigation(update() * 2 + update().replace("camera=-10,-5,2", "camera=-30,-5,2"))

    def test_pan_and_zoom_geometry(self):
        text = (update() + update().replace("camera=-10,-5,2", "camera=-30,-25,2")
                .replace("indicator=12.25,9.75,51.5,26.5", "indicator=22.25,19.75,51.5,26.5")
                + update().replace("camera=-10,-5,2", "camera=-10,-5,4")
                .replace("indicator=12.25,9.75,51.5,26.5", "indicator=9.75,8.5,26.5,14"))
        self.assertTrue(verify_navigation(text)["overlay_geometry_verified"])

    def test_fractional_preview_with_clipped_camera(self):
        self.assertEqual((7.25, 7.5, 202.0, 101.75), verify_geometry(
            " camera=10,20,1 viewport=300,200 overview=216.5,116.75"
            " artboards=0,0,200,100 indicator=7.25,7.5,202,101.75"))

    def test_missing_invalid_or_nonfinite_geometry_fails(self):
        for text in ("", " camera=0,0,0 viewport=1,1 overview=100,100 artboards=0,0,10,10 indicator=0,0,1,1",
                     update().replace("indicator=12.25,9.75,51.5,26.5", "indicator=nan,9.75,51.5,26.5")):
            with self.subTest(text=text), self.assertRaises(AssertionError):
                verify_geometry(text)


if __name__ == "__main__":
    unittest.main()
