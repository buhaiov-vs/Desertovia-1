# Sprite style samples

Three initial samples from `docs/sprites.md`, generated with built-in image_gen using Tiny Town and Tiny Farm as visual references.

| Sprite | Game-size PNG | Dimensions |
| --- | --- | --- |
| Wood resource | `item-wood.png` | 16 × 16 |
| Hut | `building-hut.png` | 48 × 48 |
| Citizen, male style 1 | `character-citizen-male-1.png` | 16 × 16 |

`*-source.png` preserves each generated original. `*-preview.png` is an exact 8× nearest-neighbor enlargement of the game-size image. `prompts.json` records the full generation prompts.

Game-size exports trim transparent excess space and use nearest-neighbor sampling with transparent margins. Character feet align with the bottom row. The originals' alpha is preserved.

These are style trials, not integrated replacements. Generated shapes and texture are more detailed than the Kenney references. The citizen's exact recolor palette is not guaranteed; palette normalization and region verification are required before connecting it to `appearance.ts`. Building footprint support remains a separate task described in the source document. No animation frames are included.
