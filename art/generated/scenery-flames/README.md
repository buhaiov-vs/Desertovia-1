# Campfire flame animation — draft

Four individually generated frames using built-in image_gen and Tiny Town/Tiny Farm references. Exact prompts in prompts.json. Sources, individual 16x16 exports, a 64x16 horizontal strip (effect-campfire-flames.png), and 8x previews preserved. Frame order is 1–4 left to right.

Exporter samples alpha-bounded source artwork into an 8x10 area at x=4, y=6 of each cell. It keeps six transparent rows above and anchors all flames to bottom row 15. Dimensions verified. Bottom occupied pixel counts: 8, 8, 5, 6. Therefore baseline is aligned but base shape is not identical; animation needs a continuity pass. Colors include gradients beyond the intended three flat colors, plus edge artifacts. Four poses are distinct but loop smoothness and overlay on the campfire base are not yet accepted. No game integration.

Count this as four generated frame assets, not five: the strip is their assembled deliverable.
