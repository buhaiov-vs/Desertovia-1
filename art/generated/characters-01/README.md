# Citizen bodies — draft batch 1

Three separate built-in image_gen generations using Tiny Town and Tiny Farm as references: male style 2 (vest and foot wraps), male style 3 (work apron), female style 1 (braid and sarafan). Exact prompts and reference paths are in prompts.json. Original sources, 16x16 PNG exports and 8x nearest-neighbor previews are preserved.

Exports fit the visible source proportionally into a 14-pixel-tall region, centered horizontally and aligned to the bottom. Dimensions and transparent corners pass. Native silhouettes are distinct, but facial details, foot wraps and braid detail lose definition. Source shading and edge artifacts survive sampling.

Exact recolor palette validation FAILS: male 2 has 0 of 75 opaque pixels matching the seven required colors; male 3 has 1 of 98; female 1 has 0 of 67. Exports contain 68, 83 and 59 distinct opaque RGB colors respectively. These need palette correction and pixel cleanup before use with appearance.ts. No game integration or animation performed; all three remain drafts pending final style audit.
