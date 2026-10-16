"""Read-only palette check after DragAnimationSmoke; requires Pillow and NumPy."""
import json
from pathlib import Path

import numpy as np
from PIL import Image

root = Path(__file__).resolve().parents[1]
source = Image.open(root / "assets/sprites/drag-sheet.png")
assert source.mode == "RGBA", "Use native-alpha artwork; regenerating a green backdrop softened the lines"
alpha = np.asarray(source)[:, :, 3]
# The extracted solid regions are mostly alpha 252-254 (not exactly 255).
assert (alpha == 0).mean() > .4 and (alpha >= 250).mean() > .2, "Missing native transparent background or near-opaque figure"
layout = json.loads((root / "assets/sprites/drag-animation.json").read_text())
assert layout.get("colorKey") is None, "Native-alpha artwork must bypass chroma-key decoding"
raw = np.asarray(source.convert("RGB"), dtype=float)
decoded = np.asarray(Image.open(root / "test-artifacts/drag-animation/decoded-sheet.png").convert("RGBA"), dtype=float)
manifest = json.loads((root / "assets/sprites/drag-colors.json").read_text())
for anchor in manifest["profiles"][0]["anchors"]:
    mask = (np.sum((raw - anchor["source"]) ** 2, axis=2) < 144) & (decoded[:, :, 3] > 245)
    assert mask.sum() > 100, "Insufficient palette samples"
    delta = np.median(decoded[:, :, :3][mask], axis=0) - anchor["target"]
    assert abs(delta).max() <= 3, (anchor["material"], delta)
    print(f"PASS: {anchor['material']}: RGB median delta {delta.tolist()}")
print("All ten drag palette neighborhoods are within 3 RGB levels of idle targets.")

# Validate the approved blink and pale tears survive color-key decoding.
height, width = decoded.shape[:2]
cell_width, cell_height = width // 4, height // 3
iris_counts = []
for index in range(12):
    x = round((index % 4) * width / 4)
    y = round((index // 4) * height / 3)
    cell = decoded[y:y + cell_height, x:x + cell_width]
    face = cell[int(cell_height * .22):int(cell_height * .55), int(cell_width * .20):int(cell_width * .78)]
    r, g, b, alpha = face.transpose(2, 0, 1)
    # Violet iris only; exclude cyan tears and dark navy outlines.
    iris = (alpha > 200) & (r > 50) & (g > 50) & (b > r + 35) & (b > g + 30) & (abs(g - r) < 20)
    tears = (alpha > 200) & (g > r + 12) & (b > g + 5) & (b > 155)
    iris_counts.append(int(iris.sum()))
    assert tears.sum() > 100, (index, "Pale tear details lost")
assert iris_counts[3] < iris_counts[0] * .1, "Approved closed-eye frame lost"
assert iris_counts[6] > iris_counts[3] * 3, "Eyes did not reopen"
print("PASS: closed-eye/reopening phases and opaque pale tear detail in all twelve cells.")
