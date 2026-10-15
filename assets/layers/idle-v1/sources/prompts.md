# 图层补画提示词

使用内置 image_gen；共同参考图为 assets/sprites/idle.png，透明背景。生成稿仅用作图层补画和眼睛局部参考，未替换运行素材。

## body-fill

Use case: precise-object-edit. Input 1 is the edit target: our existing transparent chibi sprite. Create the BODY ONLY animation layer by removing ALL head, face and all red-brown hair. Keep torso, neck, coat, both sleeves/hands-in-pockets, white shirt, red tie, belt, legs and shoes. Reconstruct small shoulder and upper-neck areas previously hidden beneath the hair/head, making continuous clean fabric and skin with generous hidden overlap. Keep the exact original pose, clothing, colors, shading, dark line weight, scale and coordinates. Do not redesign. Maintain the original full 1122x1402 canvas framing and empty top portion; body should remain in the LOWER HALF at exactly its original size and position, shoes unchanged. No floating hair fragments, no face, no head, no extra limbs. Genuine transparent alpha everywhere except this fully clothed body layer; no checkerboard, text, shadow, labels or background.

## rear-hair-fill

Use case: precise-object-edit. Input 1 is the edit target. Extract and complete ONLY the long REAR HAIR layer of this existing chibi sprite. Remove face, front bangs, neck, torso, clothing, hands, legs and shoes entirely. Reconstruct the back-hair mass behind the removed face and shoulders as continuous red-brown locks using the same existing hair palette and dark outlines. This is a BACK HAIR animation layer, long hair on both left and right, with filled hidden root/center overlap so a slightly moving head/body cannot reveal holes. Preserve the original visible outer back-hair silhouette, highlights, tapered tips, original scale and original position, especially left long locks from x210 to470 and right locks from x650 to965 down to y1000 on the 1122x1402 reference canvas. Keep the canvas 1122x1402 portrait framing with original empty lower leg area, not zoomed or recentered. No face/eyes/skin/clothing/front bangs. Genuine transparent background outside the hair. No labels, extra props, scenery, shadow or baked checkerboard.

## face-clean

Use case: precise-object-edit. Input 1 is the EDIT TARGET, an existing transparent chibi character. Make a clean FACE PLATE for separated animated eyes. Change ONLY the two eyes: remove both eyeballs, irises, eye whites, eyelashes and the dark eye outlines, replacing their footprints with seamless matching face skin. Retain the original eyebrows above them, blushing cheek marks, tiny mouth, face outline, nose shading, ALL hair, body and clothes exactly unchanged. The result should have blank skin in the two former eye regions, ready for eye overlays. Preserve original 1122x1402 canvas and pixel positions, scale, head tilt, palette, brightness and shading. No skin holes, no new facial features, no closed-eyelid strokes, no loss of eyebrows, no recoloring, no zoom or recenter. Genuine transparent background, no text or props.

## eyes-half-reference

Use case: precise-object-edit. Input 1 is the EDIT TARGET. Change ONLY the openness of the two eyes to the natural HALFWAY frame of a blink: upper eyelids halfway down, covering the upper half of the violet irises, lower eyelids and eye corners stay in place, a relaxed neutral expression rather than angry or skeptical. Preserve the original positions, head tilt, iris color and eye spacing; reduce the visible white and iris area naturally underneath the lowered upper lid. Entire face other than the eyes, eyebrows, cheek marks, mouth, hair, torso, clothes, feet, silhouette, brightness, original 1122x1402 canvas and framing must stay identical. This is an in-between blink frame from the same drawing, not a new illustration or new pose. Genuine transparent alpha background, no added text, props or effects.
