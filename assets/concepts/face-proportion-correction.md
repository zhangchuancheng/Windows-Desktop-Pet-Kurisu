### 2026-10-16 抱臂与叉手比例修正

用户指出两张表情与待机原图的大小，尤其脸部比例不一致。使用内置 image_gen，以 idle.png 为几何与颜色主参考，旧表情仅作为手势参考；保持 1122×1402 原生透明画布。替换 poke-fold.png、rapid.png，并为新指纹重新拟合 click-colors.json 中对应两组配色。备份位于本机 test-artifacts/face-proportions-before。

抱臂最终提示：

Production desktop pet sprite correction. Image 1 is the EDIT TARGET and exact geometry/color master (idle.png). Image 2 is ONLY a reference for the arm pose and emotional intent, NOT a reference for head/face geometry. Edit image 1 minimally. Preserve its EXACT full canvas 1122x1402, character placement, head size and tilt, hair silhouette, jaw/cheek outline, chin position, eye spacing, eye/iris size, neck, torso width, leg length, feet location, line weight, colors and shading. Keep transparency genuinely alpha. Do not crop to content. Head top around y82, feet bottom y1329 on the 1402px high canvas; retain the original margins. Especially keep the original large eyes and full rounded face from Image 1; do not flatten/shorten the face, shrink the eyes, or widen/enlarge the head. No new character design, no beautification, no relighting, no brighter skin/hair, no glow or soft blur. Crisp single navy outlines. Change ONLY the arms to a folded-arms pose across the chest as shown in image 2; adjust shirt/coat only where the arms overlap. Keep head pose exactly as image 1. Very slight annoyed/skeptical expression through eyebrows and a tiny closed mouth, with eye size still exactly the same as image 1. Output one full-body sprite only, no text, no comparison panel, no background.

叉手最终提示：

Production desktop pet sprite correction. Image 1 is the EDIT TARGET and exact geometry/color master (idle.png). Image 2 is ONLY a reference for the crossed-arms X gesture, NOT head/face geometry. Edit Image 1 minimally. Preserve EXACT original canvas 1122x1402 and every transparent margin, head top y82, foot bottom y1329, head size and tilt, hair silhouette, round jaw/cheek outline and chin position, eye spacing and LARGE EYE/IRIS SIZE, neck position, torso width, legs and feet. Do not enlarge/widen the head or shorten the face. Keep the exact original character/colors/shading/crisp navy single linework of image 1. Change ONLY the arms to an X crossed in front of her chest, both palms facing out near the collar, fingers together as in image 2. Both hands stay BELOW the chin and do not obscure the face. Keep original upper face and large eye geometry; make the eyebrows very slightly annoyed and the mouth a small open complaining mouth. Do not shrink or half close the eyes; expression should not change face proportions. Do not change any body proportions. Do not re-light, brighten, blur, soften, create glow or double outlines. One full-body sprite on true alpha transparent background only. No text/panel/green/checkerboard background. Preserve source spatial alignment precisely for switching from idle in an animation.


## 抱臂嘟脸版本

用户确认嘟脸预览后，替换 assets/sprites/poke-fold.png。图像由内置 image_gen 基于现有抱臂图编辑：仅改变轻微鼓腮、抿嘴和略带不服气的表情，保持头身比例、抱臂姿势、颜色与透明背景。旧文件保留于本机 test-artifacts/fold-before-pout。

当前 SHA-256：`32B60CCDF04151E16187EDC684F640256660EBB4F604034EE4ED12BD7B26A1DE`。
