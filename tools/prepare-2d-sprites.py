"""Build aligned runtime PNGs from the approved 2D concept drawings.

This is an art preparation tool, not a runtime dependency. It crops, resizes,
places and palette-matches the approved source drawings to the idle pose.
Requires Pillow and NumPy. Run from the repository root.
"""

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter
import numpy as np


ROOT = Path(__file__).resolve().parents[1]
CONCEPTS = ROOT / "assets" / "concepts"
SPRITES = ROOT / "assets" / "sprites"
CANVAS = (1024, 1402)
FIGURE_TOP = 43
FIGURE_HEIGHT = 1320
HEAD_WIDTH = 600

IDLE_SHEET = CONCEPTS / "kurisu-2d-states-idle-sleep-preview.png"
CLICK_SHEET = CONCEPTS / "kurisu-2d-states-click-preview.png"

SOURCES = {
    "idle.png": (IDLE_SHEET, (0, 0, 520, 750)),
    "blink-half.png": (IDLE_SHEET, (520, 0, 1010, 750)),
    "blink.png": (IDLE_SHEET, (0, 750, 520, 1558)),
    "sleep.png": (IDLE_SHEET, (520, 750, 1010, 1558)),
    "poke-block.png": (CONCEPTS / "kurisu-2d-poke-block-idle-match.png", None),
    "poke-fold.png": (CONCEPTS / "kurisu-2d-poke-fold-idle-match.png", None),
    "poke-cheek.png": (CONCEPTS / "kurisu-2d-poke-cheek-idle-match.png", None),
    "rapid.png": (CONCEPTS / "kurisu-2d-rapid-idle-match.png", None),
    "wake.png": (CONCEPTS / "sleep-idle-transition-candidate-2026-10-19.png", None),
    "drag-base.png": (CONCEPTS / "kurisu-2d-drag-source.png", None),
}

PALETTE_TARGETS = (
    "poke-block.png", "poke-fold.png", "poke-cheek.png", "rapid.png",
    "sleep.png", "wake.png", "drag-base.png",
)


def load_character(path, crop, name):
    image = Image.open(path).convert("RGBA")
    if crop is not None:
        image = image.crop(crop)
    alpha = np.asarray(image.getchannel("A"))
    yy, xx = np.nonzero(alpha >= 128)
    if not len(xx):
        raise ValueError("No opaque character in " + name)
    left, top = max(0, int(xx.min()) - 2), max(0, int(yy.min()) - 2)
    right = min(image.width, int(xx.max()) + 3)
    bottom = min(image.height, int(yy.max()) + 3)
    return image.crop((left, top, right, bottom))


def compose(image, name):
    alpha = np.asarray(image.getchannel("A"))
    opaque = alpha >= 128
    ys, xs = np.nonzero(opaque)
    top, bottom = int(ys.min()), int(ys.max()) + 1
    # A hair-width measurement at the same relative head row keeps the face
    # size consistent between sheets generated at different resolutions.
    head_row = top + round((bottom - top) * 0.24)
    head_x = np.nonzero(opaque[head_row])[0]
    head_width = int(head_x.max() - head_x.min() + 1)
    head_center = (head_x.min() + head_x.max()) / 2
    x_scale = HEAD_WIDTH / head_width
    y_scale = FIGURE_HEIGHT / (bottom - top)
    size = (round(image.width * x_scale), round(image.height * y_scale))

    # Pillow's RGBa mode filters premultiplied colors and avoids dark/red
    # halos along transparent hair and coat edges during downsampling.
    scaled = image.convert("RGBa").resize(size, Image.Resampling.LANCZOS).convert("RGBA")
    character_x = round(CANVAS[0] / 2 - head_center * x_scale)
    character_y = FIGURE_TOP - round(top * y_scale)
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    canvas.alpha_composite(scaled, (character_x, character_y))
    pixels = np.array(canvas)
    # The image generator put a few translucent, saturated red flecks at some
    # hair edges. Remove only those low-alpha specks, and fully clear almost
    # transparent pixels so later resizing cannot make a colored fringe.
    alpha = pixels[:, :, 3]
    speck = ((alpha < 80) & (pixels[:, :, 0] > 200) &
             (pixels[:, :, 1] < 120) & (pixels[:, :, 2] < 110))
    pixels[(alpha < 12) | speck] = 0
    pixels[:, :, 3][alpha >= 240] = 255
    canvas = Image.fromarray(pixels, "RGBA")
    bounds = canvas.getchannel("A").point(lambda a: 255 if a >= 128 else 0).getbbox()
    if bounds is None or bounds[0] <= 0 or bounds[1] <= 0 or bounds[2] >= CANVAS[0] or bounds[3] >= CANVAS[1]:
        raise ValueError(f"Clipped {name}: {bounds}")
    return canvas, bounds, head_width


def material_masks(pixels):
    """Locate the hair and skin in the shared 1024 x 1402 canvas.

    Color and position must both agree: the tan coat and red tie must stay out
    of the hair mask, while pale shirt and eye highlights stay out of skin.
    """
    y, x = np.indices(pixels.shape[:2])
    r, g, b = (pixels[:, :, channel].astype(np.float32) for channel in range(3))
    opaque = pixels[:, :, 3] >= 230
    rg = r / np.maximum(g, 1)
    rb = r / np.maximum(b, 1)
    gb = g / np.maximum(b, 1)
    hair_area = (y < 650) | ((y < 1000) & ((x < 355) | (x > 690)))
    hair = (opaque & hair_area & (r > 58) & (g > 25) & (b > 15)
            & (rg > 1.38) & (rb > 1.72) & (gb > 1.08))
    face_area = (y > 280) & (y < 660) & (x > 275) & (x < 745)
    skin = (opaque & face_area & (r > 175) & (g > 125) & (b > 105)
            & (rg > 1.02) & (rg < 1.28) & (gb > 1.02) & (gb < 1.30))
    return {"hair": hair, "skin": skin}


def match_idle_palette(image, idle_pixels, idle_masks, name):
    """Align material tones without changing alpha, outlines or geometry."""
    original = np.array(image)
    corrected = original.copy()
    masks = material_masks(original)
    for material in ("hair", "skin"):
        source_mask = masks[material]
        reference_mask = idle_masks[material]
        if source_mask.sum() < 1000 or reference_mask.sum() < 1000:
            raise ValueError(f"Too few {material} pixels to calibrate {name}")
        # Use robust quantiles so a different expression or a few hair shines
        # do not turn the whole pose brighter. Endpoints keep black linework
        # and near-white highlights stable.
        source = original[source_mask, :3]
        reference = idle_pixels[reference_mask, :3]
        feather = np.asarray(
            Image.fromarray(source_mask.astype(np.uint8) * 255)
            .filter(ImageFilter.GaussianBlur(1.5)), dtype=np.float32
        ) / 255.0
        # Keep even the one-pixel fringe of black contours and other materials
        # untouched; feather only controls how much classified pixels move.
        feather *= source_mask
        for channel in range(3):
            source_q = np.percentile(source[:, channel], (25, 50, 75))
            reference_q = np.percentile(reference[:, channel], (25, 50, 75))
            anchors = {}
            for old, new in zip((0, *source_q, 255), (0, *reference_q, 255)):
                anchors.setdefault(float(old), []).append(float(new))
            xp = sorted(anchors)
            fp = np.maximum.accumulate([np.mean(anchors[x]) for x in xp])
            curve = np.interp(np.arange(256), xp, fp)
            values = original[:, :, channel]
            mapped = curve[values]
            corrected[:, :, channel] = np.clip(
                np.rint(corrected[:, :, channel] * (1 - feather)
                        + mapped * feather), 0, 255
            ).astype(np.uint8)
        before = np.median(source, axis=0).astype(int)
        after = np.median(corrected[source_mask, :3], axis=0).astype(int)
        target = np.median(reference, axis=0).astype(int)
        print(f"{name} {material}: {before.tolist()} -> {after.tolist()} "
              f"(idle {target.tolist()})")
    return Image.fromarray(corrected, "RGBA")


def align_rapid_to_idle(rapid, idle):
    """Match the X-arms pose to idle's face, waist and planted feet.

    The generated pose has a slightly shorter torso and a different leg
    stance. Stretch the upper body between landmarks, then keep the approved
    idle drawing below the shorts where the gesture cannot change the pose.
    """
    source = np.asarray(rapid, dtype=np.float32) / 255.0
    premultiplied = np.empty_like(source)
    premultiplied[:, :, :3] = source[:, :, :3] * source[:, :, 3:4]
    premultiplied[:, :, 3] = source[:, :, 3]
    rows = np.arange(CANVAS[1])
    sampled_rows = np.interp(
        rows,
        (0, 43, 625, 920, 1010, 1230, 1362, 1401),
        (0, 43, 605, 880, 985, 1230, 1362, 1401),
    )
    lower = np.floor(sampled_rows).astype(int)
    upper = np.minimum(lower + 1, CANVAS[1] - 1)
    weight = (sampled_rows - lower)[:, None, None]
    warped = premultiplied[lower] * (1 - weight) + premultiplied[upper] * weight
    rgb = np.divide(
        warped[:, :, :3], warped[:, :, 3:4],
        out=np.zeros_like(warped[:, :, :3]), where=warped[:, :, 3:4] > 0.001,
    )
    pixels = np.concatenate((rgb, warped[:, :, 3:4]), axis=2)
    aligned = Image.fromarray(np.uint8(np.clip(np.rint(pixels * 255), 0, 255)), "RGBA")
    # Blend through the dark shorts. Everything below this band is exactly the
    # idle art, so rapidly switching poses cannot move the legs or boots.
    blend = np.clip((985 - rows) / 20, 0, 1)
    mask = Image.fromarray(
        np.uint8(np.rint(np.repeat(blend[:, None], CANVAS[0], axis=1) * 255)), "L"
    )
    return Image.composite(aligned, idle, mask)


def main(output_dir=SPRITES):
    output_dir.mkdir(parents=True, exist_ok=True)
    prepared = {}
    for name, (path, crop) in SOURCES.items():
        image = load_character(path, crop, name)
        canvas, bounds, head_width = compose(image, name)
        prepared[name] = canvas
        print(f"{name}: head={head_width}, bounds={bounds}, canvas={CANVAS}")
    # The concept-sheet blink poses have tiny unrelated changes in hair and
    # clothes. Keep the original standing sprite outside the two eyes.
    mask = Image.new("L", CANVAS, 0)
    draw = ImageDraw.Draw(mask)
    draw.ellipse((337, 437, 499, 530), fill=255)
    draw.ellipse((556, 432, 724, 528), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(5))
    for name in ("blink-half.png", "blink.png"):
        prepared[name] = Image.composite(prepared[name], prepared["idle.png"], mask)

    idle_pixels = np.array(prepared["idle.png"])
    idle_masks = material_masks(idle_pixels)
    for name in PALETTE_TARGETS:
        prepared[name] = match_idle_palette(
            prepared[name], idle_pixels, idle_masks, name
        )
    prepared["rapid.png"] = align_rapid_to_idle(
        prepared["rapid.png"], prepared["idle.png"]
    )

    for name, canvas in prepared.items():
        canvas.save(output_dir / name, optimize=True)
    (output_dir / "drag.png").write_bytes((output_dir / "drag-base.png").read_bytes())


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", type=Path, default=SPRITES)
    main(parser.parse_args().output_dir)
