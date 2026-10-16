# 应用图标

`app.png` 是编辑母版，`app.ico` 是 Windows 多尺寸图标（16、20、24、32、48、64、128、256 像素）。构建时将 ICO 嵌入 EXE，并复制至 dist/assets/icons 供托盘和窗口使用。

更换母版后运行 `powershell -ExecutionPolicy Bypass -File tools/build-app-icon.ps1`，再运行 `build.ps1`。母版应为正方形画布的透明 PNG。转换脚本使用 Windows 自带 System.Drawing，不调用网络服务。

来源：用户提供的截图。使用内置 image_gen 提取上方最大头像并清理背景，然后按用户要求摆正并调整为近正方形。未将原始聊天截图收录进项目。

最终编辑提示：

Edit the attached chibi girl app icon. User correction: make the character icon more square, approximately a square, rather than the currently diagonal tall shape. Straighten the head to nearly upright (remove most of its tilt), and adjust the outer hair/head silhouette into a compact rounded-square, overall visible width approximately equal to visible height. Preserve the exact annoyed half-lidded violet eyes, pink cheek blush, tiny complaining mouth, red-brown hair, bold dark brown outlines, little white collar and red tie detail, same cute flat doodle style. Keep the face/eyes aligned with the newly upright head. This is the character outline itself becoming more square, not adding a square tile behind it. Square canvas 512x512, centered visible character with 8% transparent padding all sides. True alpha transparency. No background square, no border frame, no text, no extra objects, no glow, no shadow. One clean Windows app icon.
