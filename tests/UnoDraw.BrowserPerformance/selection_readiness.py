"""Native-pixel proof that an edit's selection-clear command reached the UI."""
from __future__ import annotations

from pathlib import Path
from typing import Any

from PIL import Image, ImageChops

from quality import LUMINANCE_THRESHOLD, MAXIMUM_CHANGED_FRACTION


DESKTOP_SIZE = (1440, 960)
# The existing themed Uno 6 and Uno 7 toolbar places its selection summary here.
# Compare against this trial's unselected reference, not another build's glyphs.
SELECTION_LABEL_REGION = (4, 40, 94, 66)


def require_cleared_selection(reference: Path, current: Path) -> dict[str, Any]:
    with Image.open(reference) as before, Image.open(current) as after:
        if before.size != DESKTOP_SIZE or after.size != DESKTOP_SIZE:
            raise AssertionError("Selection readiness requires the unchanged native 1440×960 DPR1 images")
        expected = before.convert("RGB").crop(SELECTION_LABEL_REGION)
        observed = after.convert("RGB").crop(SELECTION_LABEL_REGION)
    # A blank crop cannot establish the label's state. The actual toolbar uses
    # neutral light glyphs on dark chrome in all three compared revisions.
    glyph_pixels = sum(1 for pixel in expected.get_flattened_data()
                       if min(pixel) > 100 and max(pixel) - min(pixel) < 40)
    if glyph_pixels < 30:
        raise AssertionError("The unselected reference does not contain its visible toolbar label")
    difference = ImageChops.difference(expected, observed).convert("L")
    changed = sum(count for level, count in enumerate(difference.histogram())
                  if level > LUMINANCE_THRESHOLD)
    fraction = changed / (difference.width * difference.height)
    if fraction > MAXIMUM_CHANGED_FRACTION:
        raise AssertionError(f"The selection-clear command has not reached the toolbar pixels: {fraction:.6f}")
    return {"region": list(SELECTION_LABEL_REGION), "reference_glyph_pixels": glyph_pixels,
            "changed_pixel_fraction": fraction, "luminance_threshold": LUMINANCE_THRESHOLD,
            "maximum_changed_fraction": MAXIMUM_CHANGED_FRACTION, "resized_for_comparison": False}
