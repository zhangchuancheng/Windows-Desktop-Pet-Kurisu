# 仓库内容与提交

## 提交到 Git 的内容

- `src/`：程序源码。
- `assets/`：运行立绘、七张待机图层、图层清单、可编辑底稿、提示词与说明。图片是项目原型素材，保留来源记录；本次整理没有替项目或角色素材新增授权许可证。
- `tests/`、`tools/`：可重复执行的测试和素材处理工具源码。
- `docs/`：素材说明、验收记录与精选预览。原始桌面截图不随仓库提交。
- 根目录的构建/打包脚本、台词、程序清单、配置、README 和 Git 规则。

## 仅本机保留

| 路径 | 内容 |
| --- | --- |
| `dist/` | 当前可运行程序；启动入口仍为 `dist/MakiseDesktopPet.exe` |
| `test-artifacts/` | 测试 exe、日志、截图、逐帧图片、完整时间线、测试专用配置 |
| `releases/` | 手动打包输出；之前的两个旧 ZIP 在 `releases/previous/` |
| `assets/layers/idle-v1/preview-*.png`、`check-overlap-*.png` | 可重建的素材合成和偏转检查图 |

以上由 `.gitignore` 排除，文件仍保留在本机。七张实际运行图层不被忽略；`tools/prepare-idle-layers.ps1` 可以重建图层及预览，但会覆盖派生 PNG，使用前应保留手工修改。

## 仓库与版本

公开仓库：[zhangchuancheng/Windows-Desktop-Pet-Kurisu](https://github.com/zhangchuancheng/Windows-Desktop-Pet-Kurisu)，默认分支 `main`。第二版标记为 `v2.0.0`；当前 main 另包含 2026-10-16 的拖拽动画、表情比例、嘟脸与图标更新，本次不新建标签或运行包，变更见 [CHANGELOG.md](../CHANGELOG.md)。

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

`origin` 已配置，不需要重新添加。v2.0.0 发布源码与更新说明，不附加新的运行包；之前的 ZIP 不包含第二版改动。需要运行时从源码执行 `build.ps1`；之后明确需要打包时再运行 `package.ps1`。

## 首次整理验证（2026-10-15，历史记录）

将 Git 会收录的 62 个文件（约 24.4 MiB）复制到独立目录，使用其中的 build.ps1 成功构建，确认无需本机 dist 或测试产物。四份测试源码编译通过；运行四尺寸分层像素及眨眼检查通过。PowerShell/Python 语法、Markdown 文件链接和忽略规则检查通过，八张状态立绘与七张运行图层均在提交候选中。打包脚本已同步新目录并通过语法检查，本次没有执行打包或重复三分钟验收。
