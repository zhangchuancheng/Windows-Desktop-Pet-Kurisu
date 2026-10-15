"""Measure corrected palette neighborhoods after running ClickArtSmoke.

Requires Pillow and NumPy. Reads PNGs only; never edits artwork.
These are palette checks, not proof that different poses are pixel-identical.
"""
import json
from pathlib import Path

import numpy as np
from PIL import Image

root = Path(__file__).resolve().parents[1]
output = root / "test-artifacts" / "click-refresh"
manifest = json.loads((root / "assets/sprites/click-colors.json").read_text())
report = {}
for profile in manifest["profiles"]:
    name = profile["file"]
    raw = np.asarray(Image.open(root / "assets/sprites" / name).convert("RGBA"), dtype=float)
    corrected = np.asarray(Image.open(output / name).convert("RGBA"), dtype=float)
    assert np.array_equal(raw[:, :, 3], corrected[:, :, 3]), "Alpha changed"
    materials = {}
    for anchor in profile["anchors"]:
        # Sample all nearly opaque pixels near each palette anchor, including
        # pixels outside the spatial patches used to estimate the calibration.
        mask = (np.sum((raw[:, :, :3] - anchor["source"]) ** 2, axis=2) < 144) & (raw[:, :, 3] > 245)
        assert np.count_nonzero(mask) > 100, "Insufficient palette samples"
        delta = np.median(corrected[:, :, :3][mask], axis=0) - anchor["target"]
        assert np.max(np.abs(delta)) <= 3, (name, anchor["material"], delta)
        materials[anchor["material"]] = {"rgb_median_delta": delta.tolist(), "pixels": int(np.count_nonzero(mask))}
    report[name] = materials
    print(f"PASS: {name}: ten palette neighborhoods, RGB median errors <= 3/255; alpha unchanged.")
(output / "palette-report.json").write_text(json.dumps(report, indent=2) + "\n")
