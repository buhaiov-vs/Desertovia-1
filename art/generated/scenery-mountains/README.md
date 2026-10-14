# Mountain scenery drafts

Built-in image_gen generated mountain rock terrain (16x16) and three bare peaks (32x32), using Tiny Town and Tiny Farm style references. Exact prompts in prompts.json; original sources, native exports and 8x previews preserved. Full-canvas nearest-neighbor export keeps bases on the bottom edge. All three matching mossy variants now have drafts in scenery-variants-catalog.json; exact edit prompts are included in prompts.json.

Dimensions verified; terrain top-left alpha 255 and all peak top-left corners alpha 0. Native previews show three distinct silhouettes and broad bases. Peak 1 has a stray red edge pixel; all peaks have excessive shading and fragmented details relative to the Kenney references. Terrain is too busy, cracks lose continuity, and seams have not passed validation. Overlapping peaks need a ridge-alignment audit. No game integration.

Mossy edits: 32x32 exports and transparent top-left corners verified. Comparison of alpha >=128 silhouettes shows 2 differing pixels for pair 1, 1 for pair 2, zero for pair 3. Thus pair 3 matches occupied pixels; pairs 1/2 need alignment cleanup. Peak 1 retains the red artifact; moss is muted and fine patches lose definition at native size. All style audits remain open.

