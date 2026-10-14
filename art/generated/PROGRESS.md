# Full sprite generation progress

Goal: generate the entire list in docs/sprites.md, including separate construction stages and character animation variants. Preserve rows explicitly assigned to code or official brand assets. No game integration has been performed.

## Generated so far

- All 18 resource images (samples, resources-02, resources-03).
- All 22 world building images (samples, buildings-01, buildings-02).
- All 22 building icons (building-icons), exported at 16x16 with transparent corners verified.
- Citizen, male style 1 (samples).

272 unique assets generated, including 62 construction stages, four section-5 construction props/terrain, all 26 farming drafts and all 24 village project drafts (six icons and eighteen pieces/buildings). Each has a source PNG, a game-size export and an enlarged preview. The catalog tracks generated outputs separately from final style approval. It currently has 453 document rows; construction stages and animation rows require expansion into multiple deliverables, so that is not a final image count.

## Next work

All 62 section-4 construction-stage drafts and four required section-5 images are generated; placement outline remains code-drawn as directed. Six section-6 farming bed drafts are generated. All five crops have growth stages and harvested icons generated. Section 6 has all 26 draft outputs; final style, alignment and terrain-seam audits remain open. Section 7 has all six project icon drafts. All 18 project pieces/buildings have drafts, including Great Granary (64x48) and Bell Tower (48x64). Section 7 generation pass is complete, with corrections still outstanding. Section 8 now has seven terrain drafts: three grass, stony ground, river water and both river banks. Pond water and all sixteen pond shore masks (0–15) now have drafts; pond-catalog.json tracks all combinations. Mountain rock and three bare mountain peaks now have drafts. All three mossy peak variants also have drafts. Trampled earth and both birches have drafts. Tall and young spruce plus oak now have drafts; all five tree variants are generated. Tree stump, sapling and berry bush now have drafts. Reeds and campfire base now have drafts. Four campfire flame frames and their 64x16 strip now have drafts. Gold vein ground, gold-bearing rock and depleted rock now have drafts. Monastery church and monk cell now have drafts. Gold and blue onion dome props now have drafts. Section 8 generation pass has drafts for all listed scenery, including expanded variants; final audits remain open. Section 9 now also has male style 2, male style 3 and female style 1 drafts in characters-01. Exact recolor palette checks fail; correction is required before integration. Male styles 4–5 and female style 2 now have drafts in characters-02. All seven still citizen bodies have drafts, with palette and readability corrections outstanding. Tsar and Elder now have drafts in characters-leaders; native staff/cross readability and palette correction remain open. Rabbit now has three frame drafts and a 48x16 strip in animals-rabbit; scale consistency and edge cleanup remain open. citizen-poses-catalog.json expands seven styles into 119 required frames: 28 walk, 84 work and seven asleep. Deer now has three frame drafts and a 48x16 strip in animals-deer; antler/eye readability, palette and loop alignment need correction. Bird now has three frame drafts and a 48x16 strip in animals-bird. Six-pixel width and placement match, but wing motion, eye and palette need correction. All three carried animal drafts now exist in animals-carried; deer pose and cyan edge artifact require correction, along with palette/readability and attachment audits. Section 9 animal generation pass now has all twelve required frame/assets. Male style 1 now has four walk frame drafts and a 64x16 strip in citizen-walk-male-1. A targeted v2 repair reverses the leading foot in frame 3, and shared-crop v2 exports enlarge the character to 14 pixels with contact feet at row 15. Frame 4 still lacks the passing-pose bob; exact palette and face readability remain unapproved. Four pose entries point to v2 exports, 107 remain pending. Male style 1 sowing has two frame drafts and a 32x16 strip in citizen-sow-male-1; seed visibility, horizontal alignment and palette need correction. Male style 1 carrying has two frame drafts and a 32x16 strip in citizen-carry-male-1; opposite steps are present, palette and native detail remain unapproved. Male style 1 digging now has two draft frames and a 32x16 strip in citizen-dig-male-1; tool geometry, body/foot alignment and palette remain unapproved. Male style 1 chopping now has two draft frames and a 32x16 strip in citizen-chop-male-1; body/foot alignment, facial readability and palette correction remain open. User direction: skip player skins and animations for now. Preserve existing drafts and defer all remaining player pose generation/correction. A fishing frame-1 source completed in flight and is saved without exports; not included in the count. Section 10 now has gather wood, gather stones and forage berries drafts in task-icons-01. Cut reeds, hunt rabbits and hunt deer now have drafts in task-icons-02. Hunt birds and tend fire now have drafts in task-icons-03. Fish call failed with image_gen HTTP 429 usage_limit_reached; tool-reported reset 2026-10-14 19:21:29 UTC (15:21:29 America/New_York). Next: resume icon-task-fish, then remaining task icons and non-player assets. Player work remains deferred. construction-catalog.json tracks all 62 deliverables independently; original section-4 rows stay pending until audit. Expand animation rows into their required separate deliverables. The buildings-02 exporter supports rectangular canvases and skips existing exports. All 17 new building exports passed exact dimension checks.

## Final audit remains

Review generated silhouettes at native size, simplify excess shading to match the Kenney references, enforce character recolor regions, verify terrain edges and frame alignment. In resources-03 the chain is too diagonal compared with the requested S curve; grain has a stray red edge pixel, and scroll/gear details are weak at native size. Buildings-02 drying rack has an unwanted halo; an image-tool edit (v2-source) failed to remove it and was not selected. Other new buildings have occasional colored edge pixels and more shading than the reference palette. No generated status claims these style requirements have passed.

Generation uses built-in image_gen, one call per distinct asset. Exact prompts are in each batch's prompts.json. Sources were copied from the image tool's returned paths. No CLI image generator or external API key was used.



Construction-01 uses full-canvas nearest-neighbor exports to preserve empty space. Generated stage geometry still drifts from finished references, especially lean-to supports; footprint/alignment and palette audit remains mandatory. Heap has excess perimeter framing; wood pile retains an axe and stump from its reference.


Construction-02: both drying-rack stages inherited a strong unwanted halo and require regeneration from clean references. Hut stages have structural drift and stray edge pixels; roof is more than half thatched. All eight exports preserve full canvas, but alignment has not passed audit.


Construction-03: eight drafts exported. Stone-yard ground was corrected in a separate v2 source to remove unwanted masonry; exporter selects v2 and preserves original. Woodyard ground/foundation footprints differ; stone-yard wall height, geometry across frames and colored edge pixels require audit/correction.


Construction-04: five granary drafts. Support locations drift between stages, ground string has edge artifacts, and roof stage does not retain completed log walls. Those need correction before final acceptance.


Construction-05: five Storehouse drafts. Foundation thickness, frame depth and base positions drift between stages. Ground string has colored fringe pixels. Final alignment and palette audit remains open.


Construction-06: three Farm drafts. Field v2 removes unwanted fencing; original retained. Ground and field align more closely; partial fence footprint and furrow spacing still differ. All need palette/edge cleanup and final alignment audit.


Construction-07: four Bakery drafts. Oven is unlit and roof is half thatched; foundation thickness, footprint and oven proportions drift across stages. Ground string and roof have edge artifacts; palette/alignment audit remains open.


Construction-08: three Mine drafts with distinct marked rock, excavated hole and timber supports. Rock silhouettes are similar but require exact alignment audit; palette simplification, chalk visibility and edge artifacts remain to review.


Construction-09: four Smithy drafts. Roof stage loses chimney stub and adds a gable detail; must correct. Foundation/forge geometry and base positions vary, with colored fringe pixels. Final palette/alignment audit remains open.


Construction-10: four Workshop drafts. Ground has a severe haze/halo requiring regeneration. Frame and roof supports differ, foundation geometry shifts; palette and edge cleanup remain.


Construction-11: five Library drafts. Tall frame and steep half-tiled roof present; foundation, post arrangement and heights drift between stages. Ground string has edge fringe; simplification and alignment remain open.


Construction-12: five Tavern drafts. Wall-stage masonry is too tall, foundations/post positions change across stages, and ground string has fringe artifacts. Roof draft shows completed log walls and half thatch. Final alignment/palette correction remains.

Construction-13: three Stage drafts. Ground string has colored edge artifacts; platform depth, supports and base positions drift between floor and frame. Palette simplification and exact alignment remain open.

Construction-props: four 16x16 exports verified. Peg is too small within its canvas; signboard hammer is weak; ground tile loses recognizable footprints/chips and requires simplification plus seamless-edge validation; pallet needs palette/edge cleanup. All remain drafts awaiting final audit.


Farming-beds: six 16x16 exports verified. Neighboring grass colors and furrow geometry mismatch; wet glints disappear at native size. Flat palette and seamless joins require correction. All six remain drafts.


Farming-turnip: four 16x16 transparent exports verified. Distinct growth silhouettes; neon-green fringe on sprout, excess shading, plant anchors and exposed ripe bulb need final correction/review.


Farming-carrot: four 16x16 transparent exports verified. Young foliage is oversized; sprout shape, plant anchors, bright edges and excess shades require correction. Harvested carrot and ripe shoulder remain readable at native size.


Farming-cabbage: four 16x16 transparent exports verified. Sprout leaves merge at native size; ripe/harvested folded-leaf shapes, gradients, palette, edge artifacts and plant-base alignment require final correction/review.


Farming-flax: four 16x16 transparent exports verified. Severe bright green/white sprout artifacts, inconsistent stem colors, broken thin stems and thick harvested stalks need repair; five-petal flowers lose detail at native size. Palette and alignment audit remain open.


Farming-rye: four 16x16 transparent exports verified. Sprout greens are neon, thin stalks break, ripe ears/whiskers lose definition with a red artifact, and sheaf binding needs clarity. Palette/edge/anchor audit remains open.


Project-icons-01: three 16x16 transparent exports verified. Monument crown/sceptre need stone coloring and clearer silhouette; fishing rod and palisade rope are weak at native size. Palette/edge audit remains open.


Project-icons-02: three 16x16 transparent exports verified. Bridge has extra bank detail, granary sacks merge and roof touches edge, tower is too narrow for bell/dome clarity. Palette and silhouette corrections remain open.


Project-monument: three 16x16 transparent exports verified. Pedestal undersized with weak/missing shadow, rough block too sculpted/narrow, statue details weak. All-grey material achieved; stacking alignment and palette/edge audit remain open.


Project-pier: two 16x16 transparent exports verified. Ripple rings collapse into blocks; deck source aspect differs from requested square and board divisions are weak. Six of sixteen deck edge pairs differ. Seam, proportions, palette and assembly alignment require correction.


Project-palisade-01: three 16x16 exports verified. Vertical geometry is not a repeating north-south wall; horizontal has severe halo (v2 repair unsuccessful, unselected) and inset ends. NW corner has sideways tips and scale mismatch. Geometry/transparency/joins/palette need correction.


Project-palisade-02: four 16x16 exports verified. SW corner/end scale differs from straight sections; both gate-post wall directions are wrong and require regeneration. Joins, geometry, palette and edge corrections remain open.


Project-bridge: four 16x16 exports verified. Deck edges inset; rail height/supports/end-post shapes vary, left adds bottom beam, right misses left edge and has red artifact. Assembly, seams, palette and edge repairs remain open.


Project-buildings: Great Granary 64x48 and Bell Tower 48x64 exports verified, proportional nearest-neighbor fit with margin. Required major features present. Excess shading/detail, edge artifacts, tower perspective and footprint audit remain open.


Scenery-grass: three opaque 16x16 exports generated. Palette/grid and seam audits failed: 34/36/48 colors and opposite-edge mismatches recorded in batch README. Tiny details lose shape at native size; correction required before integration.


Scenery-river: four opaque 16x16 exports generated. Bank directions are correct; palette/grid and required seams fail. Detailed edge counts in batch README. Foam loses continuity at native resolution. Optional river animation not generated.


Scenery-pond: water and shore masks 0/1/2 generated, all 16x16 opaque exports. Correct assigned corner materials, but rim width and midpoint alignment drift. Lily notch disappears at native size; palette and seam checks remain open. See batch README. Full 16-mask prompt set persisted; 13 shore tiles remain pending.


Pond masks 3–6: four drafts generated and verified 16x16 with opaque corners. Mask 5 preserves separated diagonal water. Mask 6 initial source had an erroneous horizontal shore; v2 removes it and is selected by exporter, with original preserved. All still need palette and seam alignment corrections; mask 3 transitions far from edge midpoints.


Pond masks 7–10: four 16x16 drafts exported with opaque corners. Mask 7 has sand in NW/SE instead of required water; mask 9 has an erroneous sandy branch crossing the water. Masks 8 and 10 have correct corner placement; mask 10 keeps diagonal waters disconnected. All require seam/palette correction.


Pond masks 11–15: five drafts exported, completing all sixteen mask drafts. Dimensions and opaque corners verified. Mask 11 incorrectly caps the SW water corner with shore/grass; masks 12–14 corner materials are correct but transition positions drift. Mask 15 ripple loses shape at native size. Set is generated, not seam-approved; original Pond shore document row remains pending acceptance.


Mountains: four drafts exported, dimensions and top-left alpha verified. Three silhouettes distinct; peak 1 stray red pixel, excessive shading across peaks, terrain too busy. Ridge alignment and terrain seams remain open. Mountain rock and Mountain peak rows were missing from catalog.json and have been restored from docs/sprites.md; document coverage requires reconciliation before final completion.


Mossy peaks: all three generated as edits of bare peak sources, 32x32 exports with transparent top-left corners. Alpha-threshold silhouette differences against bare variants: 2, 1, 0 pixels respectively. Peak 1 retains stray red pixel; excess shading and subdued moss readability require cleanup. Six peak variant drafts now tracked in scenery-variants-catalog.json.


Scenery-birch: three exports verified dimensions/top-left alpha. Earth v2 removes unwanted fire ring and is selected. Trees have grassy bases, excess shading and edge artifacts; tall crown too narrow, young sprite small in canvas. Terrain seams and all style audits remain open.


Scenery-trees: three sprites exported with aspect-preserving bounds fit and one-pixel margin. Dimensions/top-left alpha verified. Native previews show readable spruce and oak silhouettes; palette gradients, edge fringes and relative trunk/crown scale require final cleanup.


Scenery-woodland: stump, sapling and berry bush exported at 16x16, top-left alpha zero. Rings lose detail, sapling leaves bulky, berries oversized and shadow weak; palette and edge artifacts remain for cleanup. Source feature counts inspected, no production-ready claim.


Scenery-camp: reeds and campfire base exported 16x16, transparent top-left corners verified. Four cattail heads in source, native stalk readability/green fringe need cleanup. Campfire logs become muddled at native size; embers too flame-like. Four-frame flame effect still pending.


Scenery-flames: four source frames exported to 16x16 and assembled into a 64x16 strip (strip not double-counted). Ten-pixel-tall sampling box at x4/y6, all bases on row15. Base occupancies 8/8/5/6 reveal silhouette variation needing continuity cleanup. Palette, edge artifacts, overlay and loop audit remain open.


Scenery-gold: three 16x16 outputs, terrain opaque and rock corners transparent. Gold/depleted occupied silhouettes match at alpha threshold 128. Source glints/pick marks become weak at native size; small canvas occupancy, palette, edges and terrain seams need review.


Scenery-monastery: church 48x64 and monk cell 48x48 exported, transparent top-left corners verified. Required major features present, but facade complexity, shading and edge artifacts require simplification; dome stars/cross lose native detail. Separate dome variants remain pending.


Scenery-domes: two 16x32 exports, transparent top-left corners verified. Cross bars/stars lose definition, cross proportions large, palette and edge artifacts remain. Gold/blue geometry differs, attachment alignment pending. Both variants tracked separately.




















Catalog reconciliation (2026-10-14): all 453 current document rows are tracked. Added 28 obstacle rows and 24 Dead Lands dungeon rows; renamed Governor and Governor badge to Tsar and Tsar badge. Existing Tsar output now matches the catalog. Row status totals: 165 generated awaiting final style audit, 281 pending, seven retained as directed. Expanded variants and frames are counted separately; native generated asset count remains 272. Player skins and animations remain deferred. No new images were generated during this reconciliation because the image quota reset is still pending.

