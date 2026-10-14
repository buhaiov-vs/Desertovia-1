# Male style 1 walk — draft

Four separate built-in image_gen generations referencing the original male-1 source plus Tiny Town/Tiny Farm. Exact prompts in prompts.json. Sources, full-canvas nearest-neighbor 16x16 exports, individual 8x previews and a 64x16 horizontal strip retained. citizen-poses-catalog.json tracks these four frames independently; parent walk requirement remains incomplete until all seven styles pass.

Original exports failed animation approval: repeated leading foot, weak passing poses and undersized floating placement. Originals remain preserved.

V2: built-in image_gen targeted frame 3 limb positions (repair-prompt.json), successfully reversing the leading foot without mirroring the hair/belt. prepare-v2.ps1 samples one common crop (left 10%, top 11.25%, width/height 80%) across all frames. assemble-v2.ps1 writes a separate 64x16 strip. citizen-poses-catalog.json now selects v2 exports.

audit-v2.json verifies contact frames 1/3 occupy rows 2–15. Frame 1 left/right foot bottoms are 15/14; frame 3 is 13/15, proving opposite leading feet. Frame 2 occupies rows 1–14, but frame 4 stays at rows 2–15 and still needs its upward bob. All four exports contain zero exact matches to the seven recolor colors; palette correction remains necessary. Faces lose definition. Final loop, palette and passing-pose approval remain open. No game integration.
