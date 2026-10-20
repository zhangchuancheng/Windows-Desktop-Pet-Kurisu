# 测试

在项目根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-step4.ps1
```

需要 Windows 和 .NET Framework C# 编译器。换装测试读取 `dist/assets/outfits/`，首次运行或修改换装素材后先执行 `powershell -ExecutionPolicy Bypass -File .\build.ps1`。测试脚本不会重建或重启正式桌宠。默认测试包含约三分钟真实等待的后台状态与自然入睡验收；产物统一写到 Git 忽略的 `test-artifacts/`，首次运行会自动创建输出目录。

| 文件 | 作用 |
| --- | --- |
| RobustnessSmoke.cs | 离线模拟聊天回复，检查等待期间草稿、取消及失败恢复；使用独立配置与文件锁验证保存失败不影响操作和退出；检查拖拽捕获丢失、隐藏恢复、小工作区菜单命中与眨眼缓存复用，并输出缩放耗时。无需 Ollama。 |
| SizeSliderSmoke.cs | 后台检查菜单绘制、“切换服装”和“聊天偏好”入口、角色大小滑条、界面 80%/150% 缩放、旧版 120% 设置迁移、设置保存与焦点；可选 `--visible` 检查弹出位置、实际画面与缩放后的点击区域 |
| OutfitSwitchSmoke.cs | 使用 `build.ps1` 生成的 `dist/assets/outfits/` 后台切换七套服装，逐张核对运行素材、检查设置持久化、未知 ID 回退原版，以及缺少服装包时保留原有服装；测试设置写入独立的 `test-artifacts/outfit-settings.json` |
| IdleMotionSmoke.cs | 四尺寸新 2D 待机图、固定脚部、alpha、三档眨眼；355 像素静止与运动帧的清晰度对照及 355/500 像素短时耗时检查 |
| GazeSmoke.cs | 100/160/220/280/340/500 像素追视、透明边缘、固定脚部、恢复原色及近距离指针响应；验证八个极端方向、十六个中间方向及半闭眼不拉动眉毛或侧发，生成 `test-artifacts/screenshots/gaze-preview.png`、`gaze-angle-grid.png`、`gaze-pointer-preview-355.png`、`gaze-half-blink-355.png` 和 60 张连续渲染帧 |
| ClickArtSmoke.cs | 新 2D 点击及叉手姿态在 100/220/340/500 像素下的透明边缘、原图颜色、点击图与待机图的轮廓及鞋底对齐，以及仅眼部变化的眨眼检查 |
| SleepMotionSmoke.cs | 四尺寸睡眠呼吸、独立 ZZZ 出现与淡出、透明边缘、固定脚部和循环衔接；检查单张唤醒图、返回待机及点击/拖动衔接；后台执行 |
| DialogueSmoke.cs | 60 条台词、分组轮换、表情匹配、连点冷却、气泡尺寸和隐藏窗口状态检查 |
| RapidClickSmoke.cs | 四连击进入叉手、持续点击保持、真实等待后恢复、独立台词冷却、四尺寸及拖动/隐藏/睡眠清理；后台执行 |
| Step4Acceptance.cs | 后台状态、边缘布局、独立配置和真实三分钟入睡验收 |
| MotionSmoke.cs | 早期实际窗口回归，执行时会显示测试角色，短暂使用鼠标捕获并备份/恢复用户配置 |
| DragAnimationSmoke.cs | 七种角色尺寸（100–500 像素，含 355 像素）下单图拖拽形变的静止一致性、与待机及三张点击图的可见轮廓对齐、双向响应、停止回正、透明边缘和隐藏窗口状态检查；同时输出 80 张模拟鼠标轨迹的实际渲染帧到 `test-artifacts/drag-2d-preview-frames/` |
| OutfitDragSmoke.cs | 七套服饰在 100/355/500 像素下检查泪滴、半闭眼、闭眼、睁眼后精确还原、身体静止和缩放；对照图输出到 `test-artifacts/outfit-drag/` |
| OutfitIdleSmoke.cs | 统计七套旧眨眼图的身体差异；在 100/355/500 像素、四个呼吸时刻检查新局部眨眼的眼区外像素不变及睁眼精确还原；对照图在 `test-artifacts/outfit-idle/` |
| OutfitGazeSmoke.cs | 七套服饰在 100/355/500 像素下检查九个追视方向、睁眼/半闭眼/闭眼、轮廓透明度不变、眼区外像素不变及精确回正；对照图在 `test-artifacts/outfit-gaze/` |
| OutfitMenuInputSmoke.cs | 可选前台交互测试：另一个进程保持前台起点，使用系统鼠标输入从桌宠右键设置切换全部八种造型，覆盖置顶/置底共 16 次选择及 Esc、点击外部关闭；设置独立保存。运行期间会移动鼠标和切换前台窗口，未加入默认后台测试集。命令：`.\tests\run-step4.ps1 OutfitMenuInputSmoke` |
| run-step4.ps1 | 默认入口，运行菜单、七套换装及其拖拽表情、待机、追视、点击素材、台词、连续点击、拖拽动画、睡眠呼吸和第 4 步后台检查；可指定单个测试名称运行 |
| ChatSmoke.cs | 可选本机模型联通检查，验证偏好持久化、紧凑输入面板的 80%/150% 缩放、提示词和回复回退，并生成 `test-artifacts/chat-preview.png`、`chat-preferences-preview.png`；需要已启动 Ollama 和 `qwen3:4b-instruct` |
| ChatContextSmoke.cs | 连续轮次超出短期历史后，检查用户明确事实仍进入模型请求并可被模型召回；同时检查保存偏好后的会话清空。需要本机 Ollama 与模型 |
| encode-step4.py | 把后台测试帧编码为 GIF，需要 Python 和 Pillow |
| encode-drag-preview.py | 将拖拽渲染帧编码为 `docs/media/drag-2d-motion.gif`；需要 Python 和 Pillow，输出并非真实鼠标录屏 |

可选预览编码：

```powershell
python .\tests\encode-step4.py
```

GIF 仅为程序渲染帧预览，不是真实鼠标录屏；完整测试边界见 [验收记录](../docs/TESTING.md)。`docs/media/` 中的精选预览不会被测试自动覆盖，需要确认后手动更新。

本地试聊连接检查：`powershell -ExecutionPolicy Bypass -File .\tests\run-step4.ps1 ChatSmoke`。它会向本机模型发送两句测试输入，不改变正式桌宠的设置。

当前新图由同一组 2D 画风源图制作，运行时不再叠加旧版的自动配色校正。不同动作的明暗须通过原始 PNG 视觉检查；测试仅验证图像不会在加载时变色，并不宣称逐像素同色。
