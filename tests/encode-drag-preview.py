"""Encode actual DragWarpAnimation output for a compact visual preview.

Run DragAnimationSmoke first. This script uses Pillow only for GIF encoding;
it neither changes the runtime sprite nor simulates the deformation itself.
"""

from pathlib import Path
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
FRAMES = ROOT / "test-artifacts" / "drag-2d-preview-frames"
OUTPUT = ROOT / "docs" / "media" / "drag-2d-motion.gif"
FILES = sorted(FRAMES.glob("[0-9][0-9][0-9].png"))
if len(FILES) != 80:
    raise RuntimeError(f"Expected 80 generated drag frames, found {len(FILES)}")


def on_beige(path):
    with Image.open(path) as source:
        backdrop = Image.new("RGBA", source.size, (246, 242, 235, 255))
        backdrop.alpha_composite(source.convert("RGBA"))
        return backdrop.convert("RGB")


samples = [on_beige(FILES[i]) for i in (0, 10, 20, 30, 40, 50, 60, 70)]
reference = Image.new("RGB", (samples[0].width * len(samples), samples[0].height))
for i, sample in enumerate(samples):
    reference.paste(sample, (i * sample.width, 0))
palette = reference.quantize(colors=192)

frames = [on_beige(path).quantize(palette=palette, dither=Image.Dither.NONE)
          for path in FILES]
durations = [40 if i % 5 == 4 else 30 for i in range(len(frames))]
frames[0].save(OUTPUT, save_all=True, append_images=frames[1:],
               duration=durations, loop=0, optimize=True, disposal=2)
with Image.open(OUTPUT) as gif:
    if gif.n_frames != 80:
        raise RuntimeError(f"Unexpected GIF frame count: {gif.n_frames}")
print(f"{OUTPUT}: 80 frames, {sum(durations) / 1000:.2f} s, "
      f"{OUTPUT.stat().st_size / 1048576:.2f} MiB")
