# Hospital art proposal — day/night

No dedicated hospital was found in the current building catalog/asset set. A new
day/night concept sheet was generated from the existing medieval pixel-art style,
using the prepared bakery as a style/architecture reference (PR18).

- `hospital-day-night-source.png`: original generated transparent sheet, untouched.
- `hospital-day-draft.png` / `hospital-night-draft.png`: exact left/right crops,
  equal canvases; no resized/redrawn pixels.
- `manifest.json`: source SHA256, sizes, alpha ranges and proposed integration checks.

The hospital has a green medical sign, stone/timber walls and warm night windows.
This is a proposal, not a ready runtime asset. Before shipping: approve appearance,
normalize to the shared pixel scale/outline, align visible roof/wall/ground bounds,
check transitions and pack the existing atlas. No runtime contour shader should
be added. Original generated art is retained for further corrections.

Plan: `docs/dezertovia/HOSPITAL_PLAN.md`. No gameplay integration or deployment.
