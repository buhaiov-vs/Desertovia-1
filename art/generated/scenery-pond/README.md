# Pond terrain — in progress

Built-in image_gen, one call per tile with Tiny Town and Tiny Farm style references. Exact prompts for all seventeen planned outputs are in prompts.json. All seventeen outputs currently have drafts: pond water and shore masks 0–15. Sources, opaque 16x16 exports and 8x previews preserved. No game integration.

pond-catalog.json in the parent directory tracks all sixteen shore combinations. Mask bits: bit0 NW, bit1 NE, bit2 SE, bit3 SW; 1 means water. Binary filenames are normal high-bit-first numbers, not a visual corner ordering. Masks 5 and 10 specify disconnected diagonal water regions. All sixteen masks have drafts; correction and seam acceptance are still outstanding.

Native previews confirm corner materials for generated masks. Style and seam acceptance remain open: shore transition positions and rim widths vary, source texture introduces excess colors, and the lily pad loses its notch at 16x16. Mask 0 contains 27 colors; masks 1/2 contain 80/96; pond water contains 30. Full-grass mask 0 opposite edges mismatch at 15/16 pixels on both axes; pond water mismatches 13/16 horizontally and 14/16 vertically. Corner shore tiles are not expected to self-tile, so they need neighbor compatibility checks once the set exists. Do not treat generated status as seamless approval.

Masks 3–6 exports verified 16x16 and opaque corners. Mask 5 keeps water disconnected across the center. Mask 6 v2 corrects a spurious horizontal shore in the original, and prepare.ps1 selects v2. Exact edit prompt is in repair-0110.json. Palette and edge midpoint drift remain; mask 3 has a deep curved inlet with edge transitions too high. These are not accepted seamless tiles.


Masks 7–10: dimensions and opaque corners verified. Mask 7 fails required water corners NW and SE (sand reaches corners); mask 9 has a spurious sandy branch splitting the left water edge. Regenerate or correct these geometries. Mask 8 has the intended SW water corner; mask 10 has separated NE and SW water. Both still need pixel-grid, palette and midpoint alignment work. All remain drafts.


Masks 11–15: dimensions and opaque corners verified. Mask 11 incorrectly closes the SW water corner with grass/sand. Masks 12–14 have correct corners but nonmatching edge transitions. Mask 15 ripple loses its shape at native size. Full set still requires palette/grid simplification, geometry corrections and neighbor edge checks. The Pond shore parent document row intentionally stays pending.

