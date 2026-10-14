# Prepared night building assets — 26 objects

Requested by the owner on 2026-10-14 after the daytime sprite comparison.
This authorizes preparation of the night art; the live renderer has not been changed.

- `day-night-comparison.png`: labelled Russian gallery of all 26 pairs. Each preview
  is fitted independently for inspection; this is not a measurement of runtime scale.
- `building-*-night.png`: 26 individual transparent RGBA sprites, including warm
  windows, entry lights, nearby facade reflections and gentle cool ambient light.
- `building-*-day.png`: original crops, unchanged from `web/src/assets/sprites.png`.
- `day-sheet-{1,2}.png`: the exact reference sheets used for the lighting edits.
- `night-sheet-{1,2}.png`: original generated outputs, preserved unmodified.
- `night-atlas.png` / `night-atlas.json`: all night sprites packed in 384px cells;
  JSON includes source mappings, individual dimensions and content bounds.
- `validation.json`: asset checks and hashes of generated source sheets.
- `source-manifest.json`: original daytime frame coordinates and reference layout.

The night sprites are generated lighting edits, not pixel-identical palette swaps.
Geometry and pixel-grid detail can differ from the daytime original. Before runtime
integration, align the visible building size and ground anchor for every pair and
check transitions for jumps. The atlas must not replace the existing mixed sprite atlas.
Warm exterior glow is baked into transparency, not supplied as a separate light layer.
For smooth day/night transitions, crossfade aligned day/night layers; avoid applying
night tint twice. Player dark UI preference remains a separate UI feature.

Validation: all 26 individual night PNGs have content, RGBA alpha and transparent
background. Cell boundaries checked for visible clipping (edge alpha <= 4/255).
The boundary below the town hall was extended to retain its exterior glow and keep
it out of the workshop crop. Both original sheets remain available for re-export.
Gallery inspected visually. No gameplay, economy, auth, server data or deployment
changes; game tests were not rerun for this asset-only branch.
