# Hut construction pilot

Generated source: `hut-stages-source.png` (1254×1254 RGBA), derived from
`../night-buildings/building-hut-day.png` with image_gen on2026-10-14.
Four equal627px cells: foundation, frame, walls, roof. Original source retained.
Runtime atlas is an unchanged lossless copy, `web/src/assets/construction/hut.png`.
`frames.json` records cell rectangles and visible alpha>128 bounds.

Ground anchors and foundation width in source pixels (not canvas width):
foundation center328/base568/width412; frame300/568/424;
walls329/518/430; roof303/518/424. They are registered to a40px base,
center24, ground48 inside the author's3×3 building area and3×4 plot. Roof can overhang the base.
Generated door/window positions inherit the existing hut; final stage uses the
original day/night hut exactly. Work masks reveal each new stage above its
previous stage using completed labor jobs, independently of delivered goods.

Actual catalog stages: ground, foundation, frame, walls, roof, finish.
Ground, scaffold, ladder, material piles and one small work lantern are renderer
layers in `HutConstruction.ts`, not arbitrary old Kenney construction tiles.
Night uses the existing continuous map shade plus a separately attached lantern
emission; final-stage art shares BuildingLighting's registered day/night shader.
This keeps construction walls/window geometry fixed at night. Full building glow
is reserved for a completed structure; the work lamp has a small light pool.

No new server stages, costs, action times, resources, collisions or accounts.
Other building types retain their renderer; their preview/rising light emission
is suppressed to prevent detached duplicate light at the origin of the map.
Shared renderer cache includes current-stage jobs so delivered goods refresh.

Scope: hut pilot, not the full D-020 rollout. Further buildings and special projects
remain explicitly planned in `docs/dezertovia/CONSTRUCTION_VISUAL_PLAN.md`.
