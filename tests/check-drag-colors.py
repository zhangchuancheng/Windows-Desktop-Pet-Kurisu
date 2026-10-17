"""Read-only palette check after DragAnimationSmoke; requires Pillow and NumPy."""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

root = Path(__file__).resolve().parents[1]
source_path = root / "assets/sprites/drag-base.png"
source = Image.open(source_path)
assert source.mode == "RGBA", "Use native-alpha artwork"
layout = json.loads((root / "assets/sprites/drag-warp.json").read_text())
assert layout["image"] == source_path.name
assert list(source.size) == layout["canvas"]
manifest = json.loads((root / "assets/sprites/drag-colors.json").read_text())
profile = next(p for p in manifest["profiles"] if p["file"] == source_path.name)
assert hashlib.sha256(source_path.read_bytes()).hexdigest().upper() == profile["sha256"]

raw = np.asarray(source.convert("RGBA"), dtype=float)
decoded = np.asarray(Image.open(root / "test-artifacts/drag-warp-runtime/calibrated-base.png").convert("RGBA"), dtype=float)
assert raw.shape == decoded.shape
assert np.array_equal(raw[:, :, 3], decoded[:, :, 3]), "Color correction changed alpha"
assert (raw[:, :, 3] == 0).mean() > .4, "Transparent background was lost"
for anchor in profile["anchors"]:
    mask = (np.sum((raw[:, :, :3] - anchor["source"]) ** 2, axis=2) < 144) & (decoded[:, :, 3] > 245)
    assert mask.sum() > 20, (anchor["material"], "Insufficient palette samples")
    delta = np.median(decoded[:, :, :3][mask], axis=0) - anchor["target"]
    assert abs(delta).max() <= 4, (anchor["material"], delta)
    print(f"PASS: {anchor['material']}: RGB median delta {delta.tolist()}")
print("Drag pose keeps the idle palette within 4 RGB levels and preserves alpha.")
