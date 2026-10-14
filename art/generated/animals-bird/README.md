# Bird animation — draft

Three separate built-in image_gen calls for perched, wings-up and wings-down poses. Tiny Town and Tiny Farm are style references; frames 2–3 also reference the perched source. Exact prompts in prompts.json. Sources, 16x16 exports and 8x previews retained. character-bird.png is a 48x16 horizontal strip without gaps.

prepare.ps1 samples the same centered half-width/half-height crop of each source using nearest-neighbor, maintaining source placement rather than independently fitting each bird. All three exports have alpha>=128 bounds x=5..10, y=6..10 (six pixels wide), transparent corners and 16x16 dimensions. The red breast remains visible. Native review fails animation readability: the raised/lowered wings mostly collapse into the same silhouette, the eye/beak lose clarity, and frame 3 has an olive edge pixel. Palette, eye and wing silhouettes need correction before use. No game integration.
