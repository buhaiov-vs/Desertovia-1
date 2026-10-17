# 0.7.11 fence atlas — integrated on feature branch

Owner D-097: keep original outlines, palette, pixel scale, perspective and night.
Generated with image_gen17October2026 from actual tiny-town.png and night building
references. revision-source.png1254x1254 is source artwork. reference-layout.png
is the original tile layout shown to image_gen. No third-party pack/license added.

Rejected draft-source.png2172x724 remains as history, never loaded by the game.
Revision source is mechanically cropped/padded/nearest-resized by pack.mjs into
web/src/assets/fences/day.png and night.png,128x64 each. Source rectangles and
fallback aliases explicit in script; north/south straight share a continuous frame.
The native reference tile69 was a gate, so not used as a straight horizontal.

Run from web directory: node ../art/generated/fence-0-7-11/pack.mjs --export;
then node ../art/generated/fence-0-7-11/check-atlas.mjs. Script checks32frames,
straight left/right and top/bottom edge masks and day/night silhouette drift.
Latest masks44/4283 (1.03%), all straight joins PASS. Fully transparent pixels may
retain irrelevant RGB; inspect over grass and alpha, not RGB without alpha.

FortificationView uses matching frame positions, opaque day base plus night overlay
through existing nightAmount. No stone emission. Advanced damage adds small pixel
cracks without breaking joins; destroyed pieces use rubble. Open gates remove only
physical gate cells, no painted fake wall. Level1 uses original Kenney textures.

Actual VillageWorld map inspected on phone and desktop: log/stone day/night,
corner joins, hut/StoneYard relative scale and river endcaps. Browser harness uses
real DTO contour and renderer, not an illustrated mockup. See tests fences.spec.ts
for map screenshots, dusk opacity, gate and static-cache identity checks. These are
branch checks; no claim that live server or physical-phone performance changed.
