"""Deterministic material-specific color correction; geometry and alpha stay exact.

Generate a review copy first. --apply copies reviewed results to runtime sources,
after saving byte-identical originals in the output directory. Never use generated
images as calibration inputs for another pass.
"""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
PACKS = ROOT / 'assets/concepts/outfit-actions-2026-10-19'
OUTFITS = ['lab-coat', 'maid', 'yukata', 'christmas-v2',
           'red-dress-handfix', 'beachwear-v2', 'battle-suit']
POSES = ['idle.png', 'blink-half.png', 'blink.png', 'poke-block.png',
         'poke-fold.png', 'poke-cheek.png', 'rapid.png', 'sleep.png',
         'wake.png', 'drag.png', 'drag-base.png']


def masks(p):
    y, x = np.indices(p.shape[:2])
    r, g, b = np.moveaxis(p[:, :, :3].astype(float), 2, 0)
    hi, lo = np.maximum.reduce([r, g, b]), np.minimum.reduce([r, g, b])
    sat = (hi-lo)/np.maximum(hi, 1)
    opaque = p[:, :, 3] >= 230
    # Restrict hue as well as location: red hats/dresses and golden accessories
    # must not be treated as chestnut hair, even when they overlap the head.
    hair = (opaque & (r > 62) & (g > 28) & (b > 20) & (r/g.clip(1) > 1.45)
            & (r/g.clip(1) < 2.65) & (g/b.clip(1) > 1.10)
            & (g/b.clip(1) < 1.65) & (r/b.clip(1) > 1.85)
            & ((y < 610) | ((y < 1020) & ((x < 350) | (x > 700)))))
    skin = (opaque & (r > 200) & (g > 156) & (b > 128)
            & (r/g.clip(1) > 1.035) & (r/g.clip(1) < 1.28)
            & (g/b.clip(1) > 1.035) & (g/b.clip(1) < 1.28))
    crown = (y > 180) & (y < 390) & (x > 260) & (x < 760)
    face = (y > 490) & (y < 585) & (x > 370) & (x < 660)
    body = opaque & (y > 620) & ~hair & ~skin
    return {'hair': hair, 'skin': skin,
            'hair_sample': hair & crown, 'skin_sample': skin & face,
            'cloth_white': body & (hi > 170) & (sat < .13),
            'cloth_dark': body & (hi < 115) & (hi > 38) & (sat < .25) & (y < 1170),
            'shoes': body & (hi < 115) & (hi > 38) & (sat < .25) & (y >= 1170),
            'cloth_red': body & (r > 75) & (r/g.clip(1) > 2.8) & (r/b.clip(1) > 2.1),
            'cloth_purple': body & (b > 60) & (b/g.clip(1) > 1.25) & (r/g.clip(1) > 1.1) & (r/b.clip(1) < 1.4),
            'cloth_green': body & (g/r.clip(1) > 1.18) & (g/b.clip(1) > 1.06) & (g > 45)}


def quantiles(p, mask):
    return np.percentile(p[mask, :3], [25, 50, 75], axis=0)


def correct(p, reference, idle):
    own, ref, clothes = masks(p), masks(reference), masks(idle)
    out = p.copy()
    metrics = {}
    for material in ['hair', 'skin', 'cloth_white', 'cloth_dark', 'shoes',
                     'cloth_red', 'cloth_purple', 'cloth_green']:
        sample = material + '_sample' if material in ('hair', 'skin') else material
        target = reference if material in ('hair', 'skin') else idle
        target_masks = ref if material in ('hair', 'skin') else clothes
        src_mask, dst_mask = own[sample], target_masks[sample]
        if min(src_mask.sum(), dst_mask.sum()) < 180:
            continue
        before, desired = quantiles(p, src_mask), quantiles(target, dst_mask)
        # A robust constant offset preserves local contrast and the shape of
        # every painted shadow. Small clothing offsets avoid overfitting poses.
        limit = 12 if material == 'hair' else (8 if material == 'skin' else 6)
        shift = np.clip(np.median(desired-before, axis=0), -limit, limit)
        mask = own[material]
        feather = np.asarray(Image.fromarray((mask*255).astype('uint8'))
                             .filter(ImageFilter.GaussianBlur(1.2)), dtype=float)/255
        weight = feather * mask
        # Fade into dark linework and bright highlights, never change alpha.
        if material == 'hair':
            weight *= np.clip((p[:, :, 0].astype(float)-62)/35, 0, 1)
        out[:, :, :3] = np.clip(np.rint(out[:, :, :3].astype(float) +
                                         weight[:, :, None]*shift), 0, 255).astype('uint8')
        after = quantiles(out, src_mask)
        metrics[material] = {'shift_rgb': shift.tolist(),
            'before_median_rgb': before[1].tolist(), 'after_median_rgb': after[1].tolist(),
            'target_median_rgb': desired[1].tolist(),
            'before_error': float(np.abs(before-desired).mean()),
            'after_error': float(np.abs(after-desired).mean())}
    assert np.array_equal(p[:, :, 3], out[:, :, 3])
    assert np.array_equal(p[p[:, :, 3] < 230], out[p[:, :, 3] < 230])
    return out, metrics


def apply_reviewed(output):
    report = json.loads((output/'report.json').read_text(encoding='utf-8'))
    # Validate every input before touching any source file.
    for outfit in OUTFITS:
        for name in POSES:
            path = PACKS/outfit/name
            if hashlib.sha256(path.read_bytes()).hexdigest() != report[outfit+'/'+name]['source_sha256']:
                raise ValueError('Source changed after review: '+str(path))
            original = np.array(Image.open(path).convert('RGBA'))
            result = np.array(Image.open(output/outfit/name).convert('RGBA'))
            if original.shape != result.shape or not np.array_equal(original[:, :, 3], result[:, :, 3]):
                raise ValueError('Geometry/alpha mismatch: '+str(path))
            if not np.array_equal(original[original[:, :, 3] < 230], result[original[:, :, 3] < 230]):
                raise ValueError('Transparent edge changed: '+str(path))
    for outfit in OUTFITS:
        backup = output/'originals'/outfit
        backup.mkdir(parents=True, exist_ok=False)
        for name in POSES:
            path = PACKS/outfit/name
            shutil.copy2(path, backup/name)
            shutil.copy2(output/outfit/name, path)
    print('Applied 77 reviewed images; byte-identical originals retained in', output/'originals')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--output', type=Path, required=True)
    ap.add_argument('--apply', action='store_true')
    args = ap.parse_args()
    if (args.output/'report.json').exists():
        if args.apply:
            apply_reviewed(args.output)
            return
        raise SystemExit('Use a new output directory to preserve the original review and backups.')
    args.output.mkdir(parents=True, exist_ok=True)
    reference = np.array(Image.open(ROOT/'assets/sprites/idle.png').convert('RGBA'))
    report = {}
    for outfit in OUTFITS:
        folder = PACKS/outfit
        idle = np.array(Image.open(folder/'idle.png').convert('RGBA'))
        (args.output/outfit).mkdir()
        # Each outfit gets two rows: original actions, then corrected actions.
        sheet = Image.new('RGB', (8*192, 2*285), '#eeece6')
        labels = ImageDraw.Draw(sheet)
        show = ['idle.png', 'poke-block.png', 'poke-fold.png', 'poke-cheek.png',
                'rapid.png', 'sleep.png', 'wake.png', 'drag.png']
        for name in POSES:
            path = folder/name
            p = np.array(Image.open(path).convert('RGBA'))
            corrected, metrics = correct(p, reference, idle)
            Image.fromarray(corrected).save(args.output/outfit/name)
            report[outfit+'/'+name] = {'source_sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                                      'materials': metrics, 'alpha_exact': True, 'edge_pixels_exact': True}
            if name in show:
                x = show.index(name)*192
                for row, pixels in enumerate([p, corrected]):
                    thumb = Image.fromarray(pixels).resize((187, 256), Image.Resampling.LANCZOS)
                    sheet.paste(thumb, (x, row*285+24), thumb)
                    labels.text((x+4, row*285+5), ('before ' if row == 0 else 'after ')+name, fill='black')
        sheet.save(args.output/(outfit+'-comparison.jpg'), quality=94)
    (args.output/'report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    if args.apply:
        apply_reviewed(args.output)
    for material in ['hair', 'skin', 'cloth_white', 'cloth_dark', 'cloth_red', 'cloth_purple']:
        rows = [r['materials'][material] for r in report.values() if material in r['materials']]
        print(material, len(rows), 'mean quantile RGB error',
              round(np.mean([r['before_error'] for r in rows]), 2), '->',
              round(np.mean([r['after_error'] for r in rows]), 2))


if __name__ == '__main__':
    main()
