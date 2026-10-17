# 测试

在项目根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-step4.ps1
```

需要 Windows 和 .NET Framework C# 编译器。脚本先进行四尺寸分层像素检查，再执行约三分半钟的后台状态与自然入睡验收；不会重建或重启正式桌宠。测试产物统一写到 Git 忽略的 `test-artifacts/`，首次运行会自动创建输出目录。

| 文件 | 作用 |
| --- | --- |
| LayeredIdleSmoke.cs | 四尺寸分层图、固定脚部、alpha 和眨眼阶段检查 |
| GazeSmoke.cs | 四尺寸追视、透明边缘、固定脚部、恢复原色及指针转向平滑检查；生成 `test-artifacts/screenshots/gaze-preview.png` |
| ClickArtSmoke.cs | 三张普通点击及叉手姿态的颜色校准、alpha 不变、替换素材不误校准及四尺寸透明边缘检查 |
| check-click-colors.py | 运行 ClickArtSmoke 后检查十类色彩邻域的 RGB 偏差，需 Pillow、NumPy；只读素材 |
| SleepMotionSmoke.cs | 四尺寸睡眠呼吸、独立 ZZZ 出现与淡出、透明边缘、固定脚部、循环衔接和睡醒状态切换；后台执行 |
| DialogueSmoke.cs | 60 条台词、分组轮换、表情匹配、连点冷却、气泡尺寸和隐藏窗口状态检查 |
| RapidClickSmoke.cs | 四连击进入叉手、持续点击保持、真实等待后恢复、独立台词冷却、四尺寸及拖动/隐藏/睡眠清理；后台执行 |
| Step4Acceptance.cs | 后台状态、边缘布局、独立配置和真实三分钟入睡验收 |
| MotionSmoke.cs | 早期实际窗口回归，执行时会显示测试角色，短暂使用鼠标捕获并备份/恢复用户配置 |
| DragAnimationSmoke.cs | 替代旧方向镜像测试；四尺寸十二帧、透明边缘、绿幕残留、循环与缓存，以及隐藏窗口拖动阈值、四方向、松手恢复检查 |
| check-drag-colors.py | 运行 DragAnimationSmoke 后检查拖拽动作表十类 RGB 配色、闭眼与睁眼及浅色泪珠保留，需 Pillow、NumPy，只读素材 |
| run-step4.ps1 | 默认入口，运行分层、追视、点击素材、台词、连续点击、拖拽动画、睡眠呼吸和第 4 步八项后台检查 |
| encode-step4.py | 把后台测试帧编码为 GIF，需要 Python 和 Pillow |

可选预览编码：

```powershell
python .\tests\encode-step4.py
```

GIF 仅为程序渲染帧预览，不是真实鼠标录屏；完整测试边界见 [验收记录](../docs/TESTING.md)。`docs/media/` 中的精选预览不会被测试自动覆盖，需要确认后手动更新。

## 当前配色辅助检查的已知偏差

`check-click-colors.py` 保留各通道 3/255 的严格阈值；当前嘟脸图的虹膜渐变邻域测得 [4,3,1]，因此该可选脚本会报此项失败。没有放宽阈值或宣称全部配色检查通过；主要色块及 alpha 检查正常，详见验收记录。`check-drag-colors.py` 的当前拖拽配色检查通过。
