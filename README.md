# Windows 桌宠 · 牧濑红莉栖

<img src="assets/icons/app.png" alt="桌宠图标" width="96" />

透明背景、无边框的 Windows 桌面小挂件：会眨眼、呼吸、回应点击，也会在安静时打瞌睡。日常运行完全离线，无需登录、管理员权限或付费 API。

**当前 `main` 包含 2026-10-16 的后续更新，功能和素材已比 v2.0.0 发布版更新。** 获取当前效果请下载本分支源码并构建；现有 Release 和旧 ZIP 不包含本次更新。本次提交不新增版本标签或运行包。

![当前待机与点击姿态](docs/media/click-poses.png)

从左到右：待机、抬手挡戳、抱臂嘟脸、轻捂脸颊、连续点击叉手。图片由实际渲染器输出，浅色底仅用于展示透明边缘。

## 本次更新

- **十二帧拖拽动画**：悬垂、小幅扑腾、眯眼眨眼和滴泪；每帧 80 毫秒，960 毫秒循环。
- 拖动各方向共用动作，转向不翻转、不重播；按住不移动也继续播放，松开恢复待机。
- 修复拖拽素材单帧发虚，使用原生透明动作表并调整缩放方式。
- 抱臂与叉手图按待机底稿修正头脸比例；抱臂加入用户确认的嘟脸表情，重新校准配色。
- 更换近正方形头像图标，统一应用与托盘图标，提供 16–256 像素 ICO。

<details>
<summary>查看当前拖拽动作分帧</summary>

![当前拖拽动作分帧](docs/media/drag-poses.png)

第一格为待机参考，随后按行展示十二帧拖拽动作。这是程序渲染图，不是桌面录屏。

</details>

完整记录见 [CHANGELOG.md](CHANGELOG.md)。保留的功能包括分层待机、动态 ZZZ 睡眠、60 条原创中文台词、置顶/置底、托盘与设置保存。

## 快速开始

环境：**Windows 10/11、.NET Framework 4.8、Windows PowerShell**。无需安装 .NET SDK 或 Python。

下载源码并解压，在项目文件夹打开 PowerShell，执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\dist\MakiseDesktopPet.exe
```

重新构建前请从托盘退出正在运行的桌宠。构建使用系统自带的 .NET Framework C# 编译器，输出到 `dist/`；之后可以直接双击其中的 `MakiseDesktopPet.exe`。

请保留 EXE 旁的 `MakiseDesktopPet.exe.config`、`dialogues.json` 和整个 `assets/` 文件夹（包括 sprites、layers、icons）。默认不会设置开机启动。

## 操作

| 操作 | 效果 |
| --- | --- |
| 普通左键点击 | 三种表情依次循环，伴随轻微回弹和约 5 秒的中文气泡 |
| 1.8 秒内点击 4 次 | 进入叉手姿态；继续点击保持，停点约 1.2 秒后恢复 |
| 按住并移动 | 超过 6 像素开始拖拽；松开保存位置，不误触点击 |
| 3 分钟不与桌宠互动 | 自动睡觉，缓慢呼吸并出现飘动的 ZZZ；点击或拖动唤醒 |
| 右键角色 | 换一句话、大小、置顶/置底、隐藏、退出 |
| 托盘右键 / 双击 | 显示、隐藏、退出 / 恢复显示 |

- 初始高度 220 像素，支持 160 / 220 / 280 / 340 像素。首次启动位于主屏幕右下角。
- 窗口不抢键盘焦点；只统计桌宠自身互动，不监控其他程序的键盘输入。
- 连续点击的特别吐槽有独立 20 秒冷却，保持叉手时不反复刷新气泡。
- 隐藏仍会在托盘运行，选择“退出”才会结束进程。置底表示位于普通窗口后方。
- 位置、大小、窗口层级保存在 `%LOCALAPPDATA%\MakiseDesktopPet\settings.json`；显示区域变化时会将角色移回可见区域。

立即预览睡眠：先退出桌宠，再运行 `.\dist\MakiseDesktopPet.exe --preview-sleep`。

## 素材与开发

素材与逻辑分离。角色图根据用户提供的参考制作，属于 AI 辅助制作的项目原型素材；生成过程保存在 `assets/concepts/`，不是官方美术或官方授权声明。

| 路径 | 内容 |
| --- | --- |
| `src/` | C# / Windows Forms 源码 |
| `assets/sprites/` | 状态图、拖拽动作表、配色配置 |
| `assets/layers/idle-v1/` | 分层待机和三档眼睛素材 |
| `assets/icons/` | 图标母版、多尺寸 ICO 与说明 |
| `dialogues.json` | 60 条本地原创台词 |
| `tests/`、`tools/` | 测试及素材处理工具 |
| `docs/` | 替换说明、测试记录和精选预览 |
| `dist/`、`test-artifacts/`、`releases/` | 本地构建、测试、压缩包，均不提交 Git |

[素材、分层动画与台词替换](docs/ASSETS.md) · [测试运行方法](tests/README.md) · [仓库说明](docs/REPOSITORY.md)

当前采用分层图片与程序小幅变换、整张状态图和十二帧动画，不使用 Live2D。没有行走、攀爬、桌面识别、联网聊天或语音。不同状态仍可能有细微轮廓差异，拖动松手缓动尚未实现。

## 验证范围

开发与测试环境为 Windows 11、150% 显示缩放、.NET Framework 4.8。后台测试覆盖四种尺寸、透明边缘、眨眼、点击与连击、拖拽状态、睡眠呼吸和 ZZZ；自然三分钟入睡及焦点、托盘的历史实测记录见 [TESTING.md](docs/TESTING.md)。

本次推送前已完成提交文件的独立构建，以及分层待机、点击素材、台词、连击、拖拽和睡眠六项后台回归。嘟脸图虹膜渐变的 RGB 邻域检查有约 4/255 的最大偏差，略超辅助检查的 3/255 阈值，已记入验收记录；其余主要色块已校准。这不等于所有状态的颜色逐像素相同。

后台验证不代替鼠标手感、系统托盘的前台验收；多显示器拔插、另一台 Windows 机器和长期稳定性仍待验证。较早的 [动作节选](docs/media/motion-preview.gif) 与 [尺寸对照](docs/media/four-sizes.png) 属于历史预览，不代表当前全部素材与动作。

## 手动打包

开发期间无需打包。本次只提交源码和文档。需要可解压运行的 ZIP 时，在退出桌宠后执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
```

输出位于 `releases/`，包含运行包和源码包；无需联网。素材替换或代码修改后，应重新构建对应运行程序。
