# Building and object sprite overview

[Open the complete PNG](overview.png) — 3200 × 9621 pixels.

Collected from the current repository:19 tile-composed buildings/project structures,
239 non-character frames in the prepared atlas, and7 complete source sheets.
The prepared art is labeled separately from the tile-based renderer. Raw source
sheets also contain character tiles. Procedural shelters, racks, piles, stage and
rope bridges do not have standalone PNG sprites in the current renderer.

Images preserve their original pixels and use nearest-neighbor enlargement.
Sources: web/src/game/worldMap.ts, web/src/assets/sprites.json and sprites.png,
web/public/assets/kenney/*.png and web/public/assets/extras.png.

## Current renderer versus prepared sprites

[Open the paired comparison](comparison.png):26 corresponding buildings,
including the procedurally drawn shelters/racks/piles/stage and monastery scenery.
The left side was exported through the actual Pixi drawing methods. The right
side contains the corresponding atlas frames. Both use4 screen pixels per world
pixel; night shading, labels and seasonal crop overlays are excluded.
