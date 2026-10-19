# 服饰动作配色校准

七套服饰的头发与肤色以 `assets/sprites/idle.png` 为共同参考；衣服和鞋子的颜色以各套自己的待机图为参考，不互相套用服装配色。

`tools/harmonize-outfit-colors.py` 使用位置与颜色共同划分头发、皮肤、白色面料、深色面料、红色、紫色、绿色面料和鞋子。刘海与面颊的固定区域用于估计角色主色，衣物则在自己的材质区域内取样。根据 25%、50%、75% 分位数估计稳健 RGB 偏移，不移动像素、不重画动作，也不重设阴影的形状。每通道最大改动：头发 12、皮肤 8、衣物 6（范围 0–255），边界处减弱处理。

Alpha 通道和 alpha 小于 230 的像素保持逐字节不变，避免引入新的透明边缘色晕。原版素材与七套服饰的动画坐标配置不修改。旧整图眨眼素材也同步校色，但程序待机仍使用局部眨眼。

先生成对照图和 `report.json`，检查后再应用同一批结果：

```powershell
python tools/harmonize-outfit-colors.py --output test-artifacts/outfit-colors-review
python tools/harmonize-outfit-colors.py --output test-artifacts/outfit-colors-review --apply
```

需要 Python、NumPy 和 Pillow。输出目录不应复用；应用前核对源文件 SHA-256、画布尺寸、透明度与透明边缘，并在该目录的 `originals/` 保留原图备份。报告中的 RGB 分位差只衡量采样区域的配色接近程度，不是感知色差或画风一致性评分。

此修正处理色调差异，独立绘制的阴影位置、发丝笔触与动作轮廓差异仍会保留。

## 本次结果（2026-10-20）

七套服饰共 77 张运行 PNG 完成校准。固定采样区域的 RGB 分位数平均绝对差（0–255 数值范围）如下：

| 区域 | 校正前 | 校正后 |
| --- | ---: | ---: |
| 头发 | 3.60 | 0.62 |
| 肤色 | 2.50 | 0.47 |
| 白色面料 | 4.72 | 2.39 |
| 深色面料 | 4.71 | 3.26 |
| 红色面料 | 4.35 | 2.33 |
| 紫色面料 | 6.57 | 4.48 |

七套动作前后对照图与逐图报告位于本地 `test-artifacts/outfit-colors-v1/`，旧图位于其 `originals/`。全部图片的画布、alpha 通道和半透明边缘像素通过精确相等检查。此结果不代表视觉差异完全消失。

应用后重新运行 `OutfitIdleSmoke`、`OutfitGazeSmoke`、`OutfitDragSmoke`，七套服饰的局部眨眼、追视、泪滴与精确回正均通过；77 张运行目录 PNG 的 SHA-256 与校准后源图一致。原版图片和运行代码没有改动。
