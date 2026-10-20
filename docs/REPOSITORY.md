# 仓库内容与提交

## 提交到 Git 的内容

- `src/`：程序源码。
- `assets/`：统一的 2D 运行立绘、动画坐标配置、画风源图与图标。图片是项目原型素材，保留来源记录；没有替项目或角色素材新增授权许可证。
- `tests/`、`tools/`：可重复执行的测试和素材处理工具源码。
- `docs/`：素材说明、验收记录与精选预览。原始桌面截图不随仓库提交。
- 根目录的构建/打包脚本、聊天模型安装辅助脚本、台词、桌宠聊天精简角色设定 `chat-persona.json`、程序清单、配置、README 和 Git 规则。

## 仅本机保留

| 路径 | 内容 |
| --- | --- |
| `dist/` | 当前可运行程序；启动入口仍为 `dist/MakiseDesktopPet.exe` |
| `test-artifacts/` | 测试 exe、日志、截图、逐帧图片、完整时间线、测试专用配置 |
| `releases/` | 手动打包输出；之前的两个旧 ZIP 在 `releases/previous/` |
| 本机旧版分层预览 | 旧画风检查图，可从 Git 历史重建；不参与当前程序运行 |

`dist/`、`test-artifacts/` 和 `releases/` 由 `.gitignore` 排除。原版运行图位于 `assets/sprites/`；七套换装的顶层动作图位于 `assets/concepts/outfit-actions-2026-10-19/<服装 ID>/`，构建时复制到 `dist/assets/outfits/`。`tools/prepare-2d-sprites.py` 是原版画风源图的导出工具，会覆盖对应运行 PNG，使用前应保留手工修改。

聊天“偏好”写入 `%LOCALAPPDATA%\MakiseDesktopPet\chat-preferences.json`，位于仓库外，不会随 Git 提交或运行包分发。Ollama 和模型由用户另行安装，既不加入仓库，也不放进 Release 压缩包。

## 仓库与版本

公开仓库：[zhangchuancheng/Windows-Desktop-Pet-Kurisu](https://github.com/zhangchuancheng/Windows-Desktop-Pet-Kurisu)，默认分支 `main`。当前版本为 `v3.2.1`，提供 Windows 运行包和源码包；全服饰眼球追视修复的变更见 [CHANGELOG.md](../CHANGELOG.md)。

提交作者使用 `zhangchuancheng` 及该 GitHub 账号的 noreply 邮箱。后续提交前检查本地身份与远程地址，避免将提交归属到其他账号：

```powershell
git status --short
git config user.name
git config user.email
git remote -v
git add .
git diff --cached --stat
git diff --cached --name-only
git commit -m "Describe the change"
git push origin main
```

`origin` 已配置，不需要重新添加。v3.2.1 的运行包、源码包和 SHA-256 校验文件以 GitHub Release 附件提供。ZIP 保留在本机 releases/，不加入 Git 历史；需要更新附件时重新执行 package.ps1。源码包来自 HEAD，打包前须先提交本次发布的代码与文档，确保源码包和运行包对应同一版本。

## 首次整理验证（2026-10-15，历史记录）

将 Git 会收录的 62 个文件（约 24.4 MiB）复制到独立目录，使用其中的 build.ps1 成功构建，确认无需本机 dist 或测试产物。四份测试源码编译通过；运行四尺寸分层像素及眨眼检查通过。PowerShell/Python 语法、Markdown 文件链接和忽略规则检查通过，八张状态立绘与七张运行图层均在提交候选中。打包脚本已同步新目录并通过语法检查，本次没有执行打包或重复三分钟验收。
