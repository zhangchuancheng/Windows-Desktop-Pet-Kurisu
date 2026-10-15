"""Encode application-rendered samples, retaining their measured timestamps.

Requires Pillow. Does not capture the desktop or modify character assets.
"""
import csv
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parent.parent / "test-artifacts" / "step4"
with (root / "frames.csv").open(encoding="utf-8-sig", newline="") as stream:
    samples = list(csv.DictReader(stream))

# A shared palette avoids frame-dependent palette shifts in the GIF preview.
reference = Image.new("RGB", (650 * 6, 440))
for i, index in enumerate([0, len(samples)//5, len(samples)//3,
                           len(samples)//2, len(samples)-80, len(samples)-1]):
    with Image.open(root / "frames" / samples[index]["file"]) as img:
        reference.paste(img, (650 * i, 0))
palette = reference.quantize(colors=256)

frames, durations = [], []
for i, sample in enumerate(samples):
    with Image.open(root / "frames" / sample["file"]) as img:
        frames.append(img.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE))
    end = int(samples[i+1]["time_ms"]) if i+1 < len(samples) else int(sample["time_ms"])+1000
    # GIF timestamps have 10 ms resolution; round absolute endpoints to avoid drift.
    durations.append(max(10, (round(end/10)-round(int(sample["time_ms"])/10))*10))

frames[0].save(root / "full-timeline.gif", save_all=True, append_images=frames[1:],
               duration=durations, loop=0, optimize=False, disposal=1)
selected = [i for i, s in enumerate(samples)
            if int(s["time_ms"]) < 20000 or s["stage"] in ("三分钟后入睡", "唤醒与恢复")]
short_frames = [frames[i] for i in selected]
short_frames[0].save(root / "motion-preview.gif", save_all=True,
                     append_images=short_frames[1:], duration=[durations[i] for i in selected],
                     loop=0, optimize=False, disposal=1)
for name in ("full-timeline.gif", "motion-preview.gif"):
    with Image.open(root / name) as result:
        total = 0
        for i in range(result.n_frames):
            result.seek(i)
            total += result.info["duration"]
        print(f"{name}: {result.n_frames} frames, {total/1000:.2f}s, {(root/name).stat().st_size/1048576:.2f} MiB")
print("The short preview omits the middle waiting segment; the full timeline preserves real elapsed time.")
