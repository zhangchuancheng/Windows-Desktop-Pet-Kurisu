# 测试

在项目根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-step4.ps1
```

需要 Windows 和 .NET Framework C# 编译器。脚本先进行四尺寸分层像素检查，再执行约三分半钟的后台状态与自然入睡验收；不会重建或重启正式桌宠。测试产物统一写到 Git 忽略的 `test-artifacts/`，首次运行会自动创建输出目录。

| 文件 | 作用 |
| --- | --- |
| LayeredIdleSmoke.cs | 四尺寸分层图、固定脚部、alpha 和眨眼阶段检查 |
| ClickArtSmoke.cs | 三张普通点击及叉手姿态的颜色校准、alpha 不变、替换素材不误校准及四尺寸透明边缘检查 |
| check-click-colors.py | 运行 ClickArtSmoke 后检查十类色彩邻域的 RGB 偏差，需 Pillow、NumPy；只读素材 |
| SleepMotionSmoke.cs | 四尺寸睡眠呼吸、独立 ZZZ 出现与淡出、透明边缘、固定脚部、循环衔接和睡醒状态切换；后台执行 |
| DialogueSmoke.cs | 60 条台词、分组轮换、表情匹配、连点冷却、气泡尺寸和隐藏窗口状态检查 |
| RapidClickSmoke.cs | 四连击进入叉手、持续点击保持、真实等待后恢复、独立台词冷却、四尺寸及拖动/隐藏/睡眠清理；后台执行 |
| Step4Acceptance.cs | 后台状态、边缘布局、独立配置和真实三分钟入睡验收 |
| MotionSmoke.cs | 早期实际窗口回归，执行时会显示测试角色，短暂使用鼠标捕获并备份/恢复用户配置 |
| DragDirectionSmoke.cs | 早期实际窗口方向回归，执行时会显示角色并备份/恢复用户配置 |
| run-step4.ps1 | 默认入口，运行分层、点击素材、台词、连续点击、睡眠呼吸和第 4 步六项后台检查 |
| encode-step4.py | 把后台测试帧编码为 GIF，需要 Python 和 Pillow |

可选预览编码：

```powershell
python .\tests\encode-step4.py
```

GIF 仅为程序渲染帧预览，不是真实鼠标录屏；完整测试边界见 [验收记录](../docs/TESTING.md)。`docs/media/` 中的精选预览不会被测试自动覆盖，需要确认后手动更新。
