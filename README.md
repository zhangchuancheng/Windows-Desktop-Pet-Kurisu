# Windows 桌宠 · 牧濑红莉栖

<img src="assets/icons/app.png" alt="桌宠图标" width="96" />

透明背景、无边框的 Windows 桌面小挂件：会用眼睛追随鼠标、眨眼、呼吸、回应点击，也会在安静时打瞌睡。基础互动无需联网、登录、管理员权限或付费 API；可选的本地聊天在首次下载模型后也能离线使用。

**当前版本：v2.4.0。** 普通用户请下载 Windows 运行包，完整解压后双击 `MakiseDesktopPet.exe`，无需编译。

[下载 Windows 运行包](https://github.com/zhangchuancheng/Windows-Desktop-Pet-Kurisu/releases/download/v2.4.0/MakiseDesktopPet-Windows.zip) · [发布页面与源码包](https://github.com/zhangchuancheng/Windows-Desktop-Pet-Kurisu/releases/tag/v2.4.0)

请保留程序旁的 `assets/`、`dialogues.json`、`chat-persona.json` 和配置文件，不要只移动 EXE。运行包内的“使用说明.txt”包含启动、操作、聊天设置、更新和常见问题。

## 本地聊天（可选）

右键角色或托盘图标，选择“聊两句”，即可打开只用于输入的紧凑面板。回车发送，Shift+回车换行，Esc 关闭；模型回复由角色气泡显示。只在你主动打开输入面板时接收键盘输入；普通点击仍播放 `dialogues.json` 中的本地预设台词。

首次聊天前，请先完成以下两步（约 2.5 GB 模型下载，需要联网；实际下载量以 Ollama 显示为准）：

1. 安装并启动 [Ollama for Windows](https://ollama.com/download/windows)。
2. 双击运行包中的 `Install-Chat-Model.cmd`，等待模型下载完成。也可以打开 PowerShell，手动运行 `ollama pull qwen3:4b-instruct`。然后启动桌宠并选择“聊两句”。

**模型和 Ollama 不包含在 Windows 运行包中。** `Install-Chat-Model.cmd` 及 `setup-chat.ps1` 只是调用本机 Ollama 下载模型；模型由 Ollama 保存在本机用户目录。下载完成后，桌宠仅连接本机 `127.0.0.1:11434`，聊天不需要云端账号、付费 API 或持续联网。Ollama 未运行时，输入面板会提示启动它；缺少模型时会提示运行上述 `ollama pull` 命令。未配置聊天时，其余桌宠功能仍可使用。首次加载模型可能较慢。

桌宠右键设置及托盘菜单中的“聊天偏好”可设置称呼、回复长短、感兴趣和希望避开的话题。偏好只保存在 `%LOCALAPPDATA%\MakiseDesktopPet\chat-preferences.json`；保存偏好后会清空本次对话。默认角色口吻提炼自作者的 [SillyTavern v1.4 角色卡](https://github.com/zhangchuancheng/SillyTavern-Character-Card/blob/main/makise_kurisu_ccv2_zh.json)，对应程序旁的 `chat-persona.json`；它只保留适合桌面短聊的性格与边界，修改后重启程序生效。

本次运行中最多保留最近 12 轮对话，并尝试记住少量更早的用户明确事实；退出后不保存聊天记录。长消息仍可能挤占上下文，小模型也可能答非所问或编造细节。对一部分未经证实的场景描述，程序会重试一次，必要时使用本地短句回退。

![当前待机与点击姿态](docs/media/click-poses.png)

从左到右：待机、抬手挡戳、抱臂嘟脸、轻捂脸颊、连续点击叉手。图片由实际渲染器输出，浅色底仅用于展示透明边缘。

## v2.4.0 更新

- 增加可选的本地聊天：紧凑输入面板、人物气泡回复、桌宠设置内的聊天偏好；首次使用需自行安装 Ollama 并下载模型，运行包提供下载引导脚本。
- 会话在内存中保留最近最多 12 轮，并保留少量较早的明确用户事实；关闭程序或保存偏好后清空。
- 基础动作、普通点击台词和托盘控制不依赖模型。

<details>
<summary>查看 v2.3.0 的动作与界面更新</summary>

- **连续拖拽动作**：使用一张完整的悬垂姿态图，依据鼠标移动速度连续调整双腿、双手和发梢；停下后自然回正。拖拽图已与待机、点击图对齐大小和落点，眼角泪光与滴落泪珠保留。
- **控制界面**：右键面板改为米白圆角设计；角色大小用 100–500 像素滑条调整，面板自身另可在 80%–150% 缩放；置顶和置底直接切换。
- **追视修正**：极端方向下眼睛跟随时，眉毛与侧发保持正常轮廓；眨眼仍能衔接，鼠标移动不计入互动。
- 保留三张普通点击表情、叉手连击、动态 ZZZ、近正方形图标和 60 条本地台词。

</details>

![追视的左右与正视状态](docs/media/gaze-preview.png)

上图是程序渲染的三个静态方向，浅色底仅用于展示透明边缘；实际过渡由程序平滑计算。

<details>
<summary>查看旧版十二帧拖拽记录</summary>

![旧版拖拽动作分帧](docs/media/drag-poses.png)

第一格为待机参考，随后按行展示旧版十二帧动作。这是历史程序渲染图，不代表当前拖拽画面。

旧版往返播放和泪滴的[预览记录](docs/media/drag-motion-preview.gif)保留用于对照；当前版本使用 `drag-base.png` 生成连续形变。

</details>

完整记录见 [CHANGELOG.md](CHANGELOG.md)。保留的功能包括分层待机、动态 ZZZ 睡眠、60 条原创中文台词、置顶/置底、托盘与设置保存。

## 从源码构建

环境：**Windows 10/11、.NET Framework 4.8、Windows PowerShell**。无需安装 .NET SDK 或 Python。

下载源码并解压，在项目文件夹打开 PowerShell，执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\dist\MakiseDesktopPet.exe
```

重新构建前请从托盘退出正在运行的桌宠。构建使用系统自带的 .NET Framework C# 编译器，输出到 `dist/`；之后可以直接双击其中的 `MakiseDesktopPet.exe`。

请保留 EXE 旁的 `MakiseDesktopPet.exe.config`、`dialogues.json`、`chat-persona.json` 和整个 `assets/` 文件夹（包括 sprites、layers、icons）。默认不会设置开机启动。

## 操作

| 操作 | 效果 |
| --- | --- |
| 移动鼠标 | 清醒待机时眼珠先看向指针，头部和后发稍慢地轻微跟随；不算作互动，不推迟睡觉 |
| 普通左键点击 | 三种表情依次循环，伴随轻微回弹和约 5 秒的中文气泡 |
| 1.8 秒内点击 4 次 | 进入叉手姿态；继续点击保持，停点约 1.2 秒后恢复 |
| 按住并移动 | 超过 6 像素开始拖拽；松开保存位置，不误触点击 |
| 3 分钟不与桌宠互动 | 自动睡觉，缓慢呼吸并出现飘动的 ZZZ；点击或拖动唤醒 |
| 右键角色 | 换一句话、滑动角色大小、调整界面大小、切换置顶/置底、隐藏、退出 |
| 托盘右键 / 双击 | 显示、隐藏、退出 / 恢复显示 |
| 右键角色或托盘中的“聊两句” | 打开本地聊天输入面板；回复显示在人物气泡中，需要 Ollama 和模型 |
| 右键角色或托盘中的“聊天偏好” | 设置称呼、回复长短与话题偏好，保存后开始新会话 |

- 初始高度 220 像素；右键菜单中的“角色大小”滑条从左到右可设为 100–500 像素，松开后保存。首次启动位于主屏幕右下角。
- “界面大小”用减号和加号以 10% 为步长调整右键面板，范围 80%–150%。默认 100% 相当于旧版 120% 的实际尺寸；它不改变角色大小，调整后立即保存。
- 窗口不抢键盘焦点；只统计桌宠自身互动，不监控其他程序的键盘输入。
- 鼠标追视只读取当前指针坐标，不安装全局输入钩子；点击、拖动和睡眠时自然回到正视方向。
- 连续点击的特别吐槽有独立 20 秒冷却，保持叉手时不反复刷新气泡。
- 隐藏仍会在托盘运行，选择“退出”才会结束进程。置底表示位于普通窗口后方。
- 位置、角色大小、界面大小、窗口层级保存在 `%LOCALAPPDATA%\MakiseDesktopPet\settings.json`；显示区域变化时会将角色移回可见区域。

立即预览睡眠：先退出桌宠，再运行 `.\dist\MakiseDesktopPet.exe --preview-sleep`。

## 素材与开发

素材与逻辑分离。角色图根据用户提供的参考制作，属于 AI 辅助制作的项目原型素材；生成过程保存在 `assets/concepts/`，不是官方美术或官方授权声明。

| 路径 | 内容 |
| --- | --- |
| `src/` | C# / Windows Forms 源码 |
| `assets/sprites/` | 状态图、单张拖拽底稿、配色配置 |
| `assets/layers/idle-v1/` | 分层待机和三档眼睛素材 |
| `assets/icons/` | 图标母版、多尺寸 ICO 与说明 |
| `dialogues.json` | 60 条本地原创台词 |
| `chat-persona.json` | 本地聊天用的精简角色设定；与个人偏好分开 |
| `tests/`、`tools/` | 测试及素材处理工具 |
| `docs/` | 替换说明、测试记录和精选预览 |
| `dist/`、`test-artifacts/`、`releases/` | 本地构建、测试、压缩包，均不提交 Git |

[素材、分层动画与台词替换](docs/ASSETS.md) · [测试运行方法](tests/README.md) · [仓库说明](docs/REPOSITORY.md)

当前采用分层图片与程序小幅变换、整张状态图和单图连续拖拽形变，不使用 Live2D。没有行走、攀爬、桌面识别、云端聊天或语音。不同状态仍可能有细微轮廓差异，拖动松手切回待机仍是即时切换。

## 验证范围

开发与测试环境为 Windows 11、150% 显示缩放、.NET Framework 4.8。后台测试覆盖四种尺寸、透明边缘、眨眼、点击与连击、拖拽状态、睡眠呼吸和 ZZZ；自然三分钟入睡及焦点、托盘的历史实测记录见 [TESTING.md](docs/TESTING.md)。

v2.4.0 在本机完整运行九项后台回归：四尺寸追视与眨眼、点击与连击、七尺寸拖拽、菜单设置、睡眠动画和真实计时约三分钟的自然入睡均通过。两项聊天专项检查也通过，包括本机模型回复、输入面板、偏好保存、早期用户事实的追问；模型安装脚本的已安装检测通过。两个 ZIP 完整解压，源码包独立构建、Windows 包启动响应正常。嘟脸图虹膜渐变的 RGB 邻域检查有约 4/255 的最大偏差，略超辅助检查的 3/255 阈值，已记入验收记录；其余主要色块已校准。这不等于所有状态的颜色逐像素相同。

后台验证不代替鼠标手感、系统托盘的前台验收；多显示器拔插、另一台 Windows 机器和长期稳定性仍待验证。较早的 [动作节选](docs/media/motion-preview.gif) 与 [尺寸对照](docs/media/four-sizes.png) 属于历史预览，不代表当前全部素材与动作。

连续拖拽渲染已在 100/160/220/280/340/355/500 像素下检查图像对齐、双向拖动、停下回正、透明边缘和颜色；实际鼠标拖动的手感仍以本机桌宠为准。

## 手动打包

自行制作 ZIP 时，在退出桌宠后执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
```

输出位于 `releases/`，包含运行包和源码包；无需联网。素材替换或代码修改后，应重新构建对应运行程序。
