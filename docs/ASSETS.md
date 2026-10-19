# 素材、动画与台词替换

## 角色图

当前使用用户确认的平面 2D 造型：红棕长发、灰紫色眼睛、浅棕外套、白衬衫与红领带、黑色短裤、哑光深色裤袜和短靴。运行图统一为 **1024 × 1402 RGBA PNG**，透明背景。请让人物的头部宽度、整体高度、中心线和鞋底位置在所有图中保持一致。

| `assets/sprites/` 文件 | 用途 |
| --- | --- |
| `idle.png` | 清醒待机的底稿；程序在同一张图上计算轻微呼吸、头部响应和发尾滞后 |
| `blink-half.png`、`blink.png` | 半闭眼、闭眼；只替换眼部区域，人物其余像素沿用待机图 |
| `poke-block.png`、`poke-fold.png`、`poke-cheek.png` | 三次普通点击依次切换的完整姿态 |
| `rapid.png` | 连续点击时保持的叉手姿态 |
| `sleep.png` | 睡眠姿态；程序另绘缓慢呼吸和飘动的 ZZZ |
| `wake.png` | 半睁眼、抬臂靠近抱臂姿势的单张唤醒过渡图，约 850 毫秒后切回待机 |
| `drag-base.png` | 拖拽的完整悬垂底稿；程序连续计算双腿、双手和发梢的运动及滴落泪珠 |
| `drag.png` | 当前静态拖拽底稿，原版在其上叠加动态泪滴与局部眨眼 |

当前设计源图在 `assets/concepts/kurisu-2d-*.png`。`tools/prepare-2d-sprites.py` 将源图对齐、裁切，并以 `idle.png` 为基准校准点击、连点、睡眠、唤醒和拖拽图的头发与脸部色阶，再导出对应的运行 PNG。校色保留各图原有的线条、阴影和透明轮廓。待机和两张眨眼图沿用原待机底稿；三张点击图各有一张以该底稿为造型基准的独立源图，保留不同手势和表情。`rapid.png` 使用独立叉手源图，并对齐待机的脸部和腰部位置；短裤以下沿用待机图，避免连点时双腿、鞋子跳位。半闭眼和闭眼只替换眼部。脚本只在制作素材时需要 Python、Pillow 和 NumPy；**运行和 C# 构建不需要 Python**。脚本会覆盖对应的单张运行图，手工修改后请先备份。旧版素材与分层文件可从 Git 历史找回，当前运行程序不再加载它们。

唤醒只显示单张 `wake.png`，当前源图为 `assets/concepts/sleep-idle-transition-candidate-2026-10-19.png`。更换睡姿、唤醒或待机造型时，应检查三张图的头部比例、人物中心线、透明轮廓与鞋底位置，避免切换时大小和站位跳动。

更换现有造型时，直接以相同画布、大小和文件名替换 PNG，再从托盘退出桌宠，运行 `powershell -ExecutionPolicy Bypass -File .\build.ps1`。请使用有权使用的图片。直接替换 PNG 不会触发素材制作脚本；正式发布前应检查透明边缘、同类材质明暗、眼部位置和状态切换。程序在加载时不会自动校色。若新造型的眼睛位置或轮廓不同，还需同步调整 `idle-motion.json` 中两只眼的 `eyeCenters`、`eyeApertures`（各自的上、下边界点）和 `maxShift`。

## 切换服装

右键桌宠，在设置中选择“切换服装”，可选原版，以及实验服、女仆装、浴衣、圣诞装、红礼服、运动泳装、战斗装七套服饰。选择会保存在 `%LOCALAPPDATA%\MakiseDesktopPet\settings.json`，下次启动继续使用；选择原版即可切回现有造型。

七套换装运行图位于 `assets/concepts/outfit-actions-2026-10-19/`：目录 ID 依次为 `lab-coat`、`maid`、`yukata`、`christmas-v2`、`red-dress-handfix`、`beachwear-v2`、`battle-suit`。运行构建把每套目录顶层的 11 张透明 PNG 复制到 `dist/assets/outfits/<ID>/`；生成原图与历史变体保存在本地制作目录，不属于发布源码，也不进入运行包。原版继续使用 `dist/assets/sprites/`。修改服饰图片后重新运行 `build.ps1`，或在退出桌宠后替换运行目录中的对应 PNG。

这些图已接入换装功能，但仍有美术问题。待机眨眼采用各套独立表情图，部分套装的头发、衣纹和鞋子会随眨眼切换而跳动；眼部局部追视暂不启用，拖拽使用静态姿态。普通点击、连点、睡眠与唤醒有各套对应图片。七套的静态检查和目视问题记录在 [动作图 QA 报告](../assets/concepts/outfit-actions-2026-10-19/QA-REPORT.md)及[审阅笔记](../assets/concepts/outfit-actions-2026-10-19/REVIEW-NOTES.md)。

## 原版拖拽的停用与恢复

当前原版与实验服一样，拖拽时显示各自的静态 `drag.png`，不启用身体动态形变，保留程序泪滴：双眼交错滴落，每滴约 0.54 秒并渐隐；拖拽约 1.2 秒后首次眨眼，之后每隔约 2.8 秒短暂眨眼；每轮有 30% 概率连续眨两次，两次之间睁眼约 0.11 秒，睁眼后衔接下一滴泪。原版图片自带的眼角泪光仍保留；松开后正常回到待机。

`src/PetForm.cs` 中的 `EnableOriginalDragAnimation` 当前为 `false`。`DragWarpAnimation` 在该开关关闭时仅绘制泪滴与局部眨眼；旧的身体形变实现完整保留，`assets/sprites/drag-base.png` 和 `drag-warp.json` 未改动，构建仍会保留相关素材。需要恢复时，将该开关改为 `true` 后重新构建，并相应调整 `OutfitSwitchSmoke` 中的静态拖拽断言。下文动态拖拽机制描述为保留实现的说明。

## 原版动画如何生成

- 待机、追视：`idle.png` 为唯一全身底稿。程序轻微形变上半身与头发，固定底部鞋子；`idle-motion.json` 保存眼部中心、眼眶上/下边界和最大位移。当前最大位移为原图画布上水平 10、垂直 5 像素，显示时按角色大小缩放；`GazeMotion.cs` 控制鼠标距离映射及眼睛、头部的响应速度。眼眶外和眼睑轮廓保持原图，眼眶内的原画纹理随视线轻微平移、压缩；靠近眼眶边界时仍从原画取样，不使用单色填补。回正时直接复用原图。它是 2D 眼球转向的视觉近似，不是 Live2D 模型变形。眨眼间隔约 2.8～6.8 秒，经过半闭、闭眼、半闭再睁眼。鼠标移动不计入睡眠计时。
- 普通点击：三张独立状态图循环，并有短暂的程序回弹。连续点击使用 `rapid.png`，保持到停止点击后恢复。
- 拖拽：`drag-warp.json` 指定底稿、画布、位置和泪滴起点。`src/DragWarpAnimation.cs` 根据鼠标移动与弹性阻尼连续形变双腿、双手和发梢，停止移动后回正；不循环播放多张图片，也不左右镜像。松开切回待机，不触发点击。拖拽底稿换成不同姿势后，还需重新校准代码中六个柔性作用区及泪滴起点。
- 睡眠：独立睡姿图缓慢呼吸，双脚固定。三个 Z 由程序按时间依次绘出、上飘并淡出，5.6 秒循环；唤醒后清除。
- 唤醒：点击或拖动睡着的角色时，立即清除 ZZZ 并显示 `wake.png`；约 850 毫秒后切回 `idle.png`。唤醒期间只有这一张完整姿态图，不做整图透明叠加或插帧。

程序定时器约每 16 毫秒检查一次状态。连续 3 分钟没有与桌宠互动后进入睡眠；仅移动鼠标不会重置计时。

原版待机和睡眠呼吸由程序计算细微形变；拖拽身体保持静止，眼部与泪滴单独绘制；普通点击、连点和唤醒使用独立状态图。当前没有 Live2D、行走、攀爬或桌面识别。

## 台词

`dialogues.json` 含 60 条原创本地短句，按 `click-block`、`click-fold`、`click-cheek`、`rapid`、`wake`、`drag`、`thought`、`company` 八类使用。前三类各 8 条，连点、睡醒、拖动各 8 条，思考和陪伴各 6 条。普通点击优先选当前表情对应分类；每组轮完重排，避免相邻重复。编辑 JSON 后重启程序生效。

`chat-persona.json` 是从作者的 [SillyTavern 角色卡](https://github.com/zhangchuancheng/SillyTavern-Character-Card/blob/main/makise_kurisu_ccv2_zh.json) 提炼的本地短聊设定，和固定点击台词分开。聊天偏好保存在 `%LOCALAPPDATA%\MakiseDesktopPet\chat-preferences.json`，不属于仓库文件。模型由用户另行安装，不随角色素材提供。

## 应用图标

图标母版位于 `assets/icons/app.png`，Windows ICO 位于 `assets/icons/app.ico`。修改母版后运行 `tools/build-app-icon.ps1`，再执行 `build.ps1`；详见 [图标说明](../assets/icons/README.md)。
