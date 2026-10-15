# 测试

在项目根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-step4.ps1
```

需要 Windows 和 .NET Framework C# 编译器。脚本先进行四尺寸分层像素检查，再执行约三分半钟的后台状态与自然入睡验收；不会重建或重启正式桌宠。测试产物统一写到 Git 忽略的 `test-artifacts/`，首次运行会自动创建输出目录。

| 文件 | 作用 |
| --- | --- |
| LayeredIdleSmoke.cs | 四尺寸分层图、固定脚部、alpha 和眨眼阶段检查 |
| Step4Acceptance.cs | 后台状态、边缘布局、独立配置和真实三分钟入睡验收 |
| MotionSmoke.cs | 早期实际窗口回归，执行时会显示测试角色，短暂使用鼠标捕获并备份/恢复用户配置 |
| DragDirectionSmoke.cs | 早期实际窗口方向回归，执行时会显示角色并备份/恢复用户配置 |
| run-step4.ps1 | 默认入口，只运行前两项后台检查 |
| encode-step4.py | 把后台测试帧编码为 GIF，需要 Python 和 Pillow |

可选预览编码：

```powershell
python .\tests\encode-step4.py
```

GIF 仅为程序渲染帧预览，不是真实鼠标录屏；完整测试边界见 [验收记录](../docs/TESTING.md)。`docs/media/` 中的精选预览不会被测试自动覆盖，需要确认后手动更新。
