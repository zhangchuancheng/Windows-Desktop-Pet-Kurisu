"""Read-only image QA for the seven runtime outfit action packs.

Requires Pillow and NumPy. Prints Markdown; it never edits the images.
Run from the repository root with the bundled Python runtime if needed:
    python tools/audit-outfit-actions.py > assets/concepts/outfit-actions-2026-10-19/QA-REPORT.md
"""

from __future__ import annotations

import argparse
import hashlib
import json
from collections import Counter
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_PACKS = ROOT / "assets" / "concepts" / "outfit-actions-2026-10-19"
RUNTIME_OUTFITS = (
    "battle-suit",
    "beachwear-v2",
    "christmas-v2",
    "lab-coat",
    "maid",
    "red-dress-handfix",
    "yukata",
)
REQUIRED = (
    "idle.png",
    "blink-half.png",
    "blink.png",
    "poke-block.png",
    "poke-fold.png",
    "poke-cheek.png",
    "rapid.png",
    "sleep.png",
    "wake.png",
    "drag-base.png",
    "drag.png",
)
CANVAS = (1024, 1402)
OPAQUE_THRESHOLD = 128
DIFFERENCE_THRESHOLD = 16
EYE_MARGIN = 40


def eye_mask(width: int, height: int) -> np.ndarray:
    """Exclude the configured eye apertures, with a generous 40px margin."""
    config = json.loads((ROOT / "assets" / "sprites" / "idle-motion.json").read_text("utf-8"))
    mask = np.zeros((height, width), dtype=bool)
    for aperture in config["gaze"]["eyeApertures"]:
        points = np.asarray(aperture, dtype=np.int32).reshape(-1, 2)
        left = max(0, int(points[:, 0].min()) - EYE_MARGIN)
        top = max(0, int(points[:, 1].min()) - EYE_MARGIN)
        right = min(width, int(points[:, 0].max()) + EYE_MARGIN + 1)
        bottom = min(height, int(points[:, 1].max()) + EYE_MARGIN + 1)
        mask[top:bottom, left:right] = True
    return mask


def inspect_image(path: Path) -> tuple[dict, np.ndarray | None]:
    result = {"name": path.name}
    try:
        with Image.open(path) as loaded:
            result["png"] = loaded.format == "PNG"
            result["size"] = loaded.size
            result["has_alpha"] = "A" in loaded.getbands()
            if not result["has_alpha"]:
                result["error"] = "no alpha channel"
                return result, None
            pixels = np.asarray(loaded.convert("RGBA"))
    except Exception as exc:
        result["error"] = f"cannot open: {exc}"
        return result, None

    alpha = pixels[:, :, 3]
    opaque_y, opaque_x = np.where(alpha >= OPAQUE_THRESHOLD)
    if len(opaque_x):
        result["bbox"] = (
            int(opaque_x.min()),
            int(opaque_y.min()),
            int(opaque_x.max()) + 1,
            int(opaque_y.max()) + 1,
        )
    else:
        result["error"] = "no visible character pixels"
    edges = np.concatenate((alpha[0], alpha[-1], alpha[:, 0], alpha[:, -1]))
    result["opaque_edge_count"] = int(np.count_nonzero(edges >= OPAQUE_THRESHOLD))
    result["faint_edge_count"] = int(np.count_nonzero((edges > 0) & (edges < OPAQUE_THRESHOLD)))
    result["alpha_range"] = (int(alpha.min()), int(alpha.max()))
    result["hash"] = hashlib.sha256(path.read_bytes()).hexdigest()
    return result, pixels


def outside_eye_difference(idle: np.ndarray, blink: np.ndarray) -> float:
    if idle.shape != blink.shape:
        return float("nan")
    height, width = idle.shape[:2]
    foreground = (idle[:, :, 3] >= OPAQUE_THRESHOLD) | (blink[:, :, 3] >= OPAQUE_THRESHOLD)
    sample = foreground & ~eye_mask(width, height)
    if not sample.any():
        return float("nan")
    delta = np.max(
        np.abs(idle.astype(np.int16) - blink.astype(np.int16)), axis=2
    )
    return 100.0 * float(np.count_nonzero((delta > DIFFERENCE_THRESHOLD) & sample)) / float(np.count_nonzero(sample))


def audit_pack(folder: Path) -> dict:
    names = {p.name for p in folder.glob("*.png") if p.is_file()}
    missing = [name for name in REQUIRED if name not in names]
    extras = sorted(names - set(REQUIRED))
    info: dict[str, dict] = {}
    pixels: dict[str, np.ndarray] = {}
    for name in REQUIRED:
        path = folder / name
        if not path.is_file():
            continue
        entry, array = inspect_image(path)
        info[name] = entry
        if array is not None:
            pixels[name] = array

    bad = [name for name, entry in info.items() if entry.get("error") or not entry["png"] or
           entry["size"] != CANVAS or not entry["has_alpha"] or entry["alpha_range"][0] != 0 or
           entry["opaque_edge_count"] > 0]
    wrong_sizes = Counter(
        info[name]["size"] for name in info
        if info[name].get("size") is not None and info[name]["size"] != CANVAS
    )
    alpha_faults = [
        name for name, entry in info.items()
        if not entry.get("has_alpha") or entry.get("alpha_range", (1, 1))[0] != 0
    ]
    opaque_edge_faults = [
        name for name, entry in info.items() if entry.get("opaque_edge_count", 0) > 0
    ]
    unreadable = [
        name for name, entry in info.items() if entry.get("error") or not entry.get("png", False)
    ]
    drag_match = (
        info.get("drag.png", {}).get("hash") == info.get("drag-base.png", {}).get("hash")
        if "drag.png" in info and "drag-base.png" in info else None
    )
    unique = len({info[name]["hash"] for name in REQUIRED if name != "drag.png" and
                  name in info and "hash" in info[name]})
    visible_names = [name for name in REQUIRED if name != "drag.png" and name in info and "bbox" in info[name]]
    tops = [info[name]["bbox"][1] for name in visible_names]
    bottoms = [info[name]["bbox"][3] for name in visible_names]
    blink_diff = {}
    blink_bounds = {}
    if "idle.png" in pixels:
        for name in ("blink-half.png", "blink.png"):
            if name in pixels:
                blink_diff[name] = outside_eye_difference(pixels["idle.png"], pixels[name])
                idle_bbox = info["idle.png"].get("bbox")
                blink_bbox = info[name].get("bbox")
                if idle_bbox and blink_bbox:
                    blink_bounds[name] = (
                        blink_bbox[1] - idle_bbox[1],
                        blink_bbox[3] - idle_bbox[3],
                    )

    source_dirs = (folder / "source", folder / "source-1072x1467")
    source_count = sum(
        len(list(source_dir.glob("*.png")))
        for source_dir in source_dirs
        if source_dir.is_dir()
    )
    return {
        "name": folder.name,
        "present": len(names & set(REQUIRED)),
        "missing": missing,
        "extras": extras,
        "bad": bad,
        "wrong_sizes": wrong_sizes,
        "alpha_faults": alpha_faults,
        "opaque_edge_faults": opaque_edge_faults,
        "unreadable": unreadable,
        "info": info,
        "drag_match": drag_match,
        "unique_actions": unique,
        "tops": (min(tops), max(tops)) if tops else None,
        "bottoms": (min(bottoms), max(bottoms)) if bottoms else None,
        "blink_diff": blink_diff,
        "blink_bounds": blink_bounds,
        "source_count": source_count,
        "showable": not missing and not bad and drag_match is True and unique == 10,
    }


def fmt_range(value: tuple[int, int] | None) -> str:
    return "—" if value is None else f"{value[0]}–{value[1]}"


def fmt_pct(value: float | None) -> str:
    return "—" if value is None or np.isnan(value) else f"{value:.1f}%"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("packs", nargs="?", type=Path, default=DEFAULT_PACKS,
                        help="Outfit parent directory; the default audits only the seven runtime outfits")
    args = parser.parse_args()
    if args.packs.resolve() == DEFAULT_PACKS.resolve():
        folders = [args.packs / name for name in RUNTIME_OUTFITS]
        scope = "当前运行的七套服装"
    else:
        folders = sorted(p for p in args.packs.iterdir() if p.is_dir())
        scope = "指定目录中的服装"
    audits = [audit_pack(folder) for folder in folders]
    now = datetime.now(ZoneInfo("Asia/Shanghai")).strftime("%Y-%m-%d %H:%M")

    print("# 换装动作图 QA")
    print()
    print(f"扫描时间：{now}（北京时间）。脚本只读：`tools/audit-outfit-actions.py`。")
    print()
    print(f"检查范围为{scope}，每套目录顶层的 11 张 PNG；`source/` 与 `source-1072x1467/` 原图合并计数，不参与运行画布判断。")
    print("眨眼差异：按当前 `idle-motion.json` 的两只眼轮廓外扩 40 像素作为眼区，在其余可见人物像素中统计 RGBA 任一通道相差超过 16/255 的比例。该数值用于预判整图切换闪动，不能代替目视验收。")
    print()
    print("| 装扮 | 文件 | 原图 | 尺寸、透明、边缘 | 拖拽副本 | 不透明顶部 y | 鞋底下界 y | 半眨眼眼外差异 | 闭眼眼外差异 | 可展示 |")
    print("| --- | ---: | ---: | --- | --- | --- | --- | ---: | ---: | --- |")
    for item in audits:
        if not item["bad"]:
            integrity = "通过"
        else:
            parts = []
            if item["wrong_sizes"]:
                parts.append(f"画布 {sum(item['wrong_sizes'].values())}")
            if item["alpha_faults"]:
                parts.append(f"alpha {len(item['alpha_faults'])}")
            if item["opaque_edge_faults"]:
                parts.append(f"边缘 {len(item['opaque_edge_faults'])}")
            if item["unreadable"]:
                parts.append(f"读取 {len(item['unreadable'])}")
            integrity = "待修正：" + "、".join(parts)
        drag = "一致" if item["drag_match"] is True else "缺失" if item["drag_match"] is None else "不一致"
        showable = "是" if item["showable"] else "待补齐/修正"
        print(
            f"| {item['name']} | {item['present']}/11 | {item['source_count']} | {integrity} | {drag} | "
            f"{fmt_range(item['tops'])} | {fmt_range(item['bottoms'])} | "
            f"{fmt_pct(item['blink_diff'].get('blink-half.png'))} | "
            f"{fmt_pct(item['blink_diff'].get('blink.png'))} | {showable} |"
        )
    print()
    print("“可展示”仅表示静态动作集文件齐全并通过格式/边缘检查；全部套装仍需人工核对服装一致、动作辨识、逐帧站位和运行时表现。")
    print()
    print("## 待处理事项")
    print()
    for item in audits:
        issues = []
        if item["missing"]:
            issues.append("缺少 " + "、".join(item["missing"]))
        if item["wrong_sizes"]:
            sizes = "、".join(
                f"{count} 张 {size[0]}×{size[1]}"
                for size, count in sorted(item["wrong_sizes"].items())
            )
            issues.append("画布尚需适配：" + sizes)
        if item["alpha_faults"]:
            issues.append("透明通道异常：" + "、".join(item["alpha_faults"]))
        if item["opaque_edge_faults"]:
            issues.append("人物触边：" + "、".join(item["opaque_edge_faults"]))
        if item["unreadable"]:
            issues.append("PNG 读取异常：" + "、".join(item["unreadable"]))
        if item["drag_match"] is False:
            issues.append("`drag.png` 与 `drag-base.png` 内容不同")
        if item["unique_actions"] < 10:
            issues.append(f"独立动作哈希仅 {item['unique_actions']}/10")
        for blink_name, rate in item["blink_diff"].items():
            if rate > 5:
                issues.append(f"`{blink_name}` 眼外变化 {rate:.1f}%，眨眼可能闪动，需校准为局部变化")
            shift = item["blink_bounds"].get(blink_name)
            if shift and max(abs(shift[0]), abs(shift[1])) > 8:
                issues.append(
                    f"`{blink_name}` 相对待机顶部 {shift[0]:+d}px、鞋底 {shift[1]:+d}px"
                )
        if item["tops"] and item["tops"][1] - item["tops"][0] > 12:
            issues.append(f"顶部位置跨度 {item['tops'][1]-item['tops'][0]}px，需目视检查切帧跳动")
        if item["bottoms"] and item["bottoms"][1] - item["bottoms"][0] > 12:
            issues.append(f"鞋底位置跨度 {item['bottoms'][1]-item['bottoms'][0]}px，需目视检查切帧跳动")
        if not issues:
            issues.append("静态文件检查通过；仍需人工查看动作衔接与服装细节")
        print(f"- **{item['name']}**：{'；'.join(issues)}。")
    print()
    print("## 逐张不透明边界")
    print()
    print("边界为 alpha ≥ 128 像素的 `[左, 上, 右, 下)`；“边缘”是接触画布外框的不透明像素数。")
    print()
    for item in audits:
        print(f"### {item['name']}")
        print()
        print("| 文件 | 边界 | 不透明边缘 | 低 alpha 边缘 |")
        print("| --- | --- | ---: | ---: |")
        for name in REQUIRED:
            entry = item["info"].get(name)
            if not entry:
                continue
            bbox = "—" if "bbox" not in entry else str(entry["bbox"])
            print(f"| `{name}` | {bbox} | {entry.get('opaque_edge_count', '—')} | {entry.get('faint_edge_count', '—')} |")
        if item["missing"]:
            print()
            print("缺少：" + "、".join(f"`{name}`" for name in item["missing"]) + "。")
        print()


if __name__ == "__main__":
    main()
