# Citizen bodies — draft batch 2

Male styles 4 (felt cap, belted coat) and 5 (hood, short cloak), plus female style 2 (headscarf, skirt and apron). Three separate built-in image_gen calls using the original Tiny Town and Tiny Farm references. Full prompts in prompts.json. Sources, 16x16 exports and 8x previews preserved. Exports use proportional nearest-neighbor sampling, horizontally centered and bottom-aligned.

All dimensions and transparent corners pass; feet reach the bottom row. Still-body generation now covers all seven citizen styles. These are not approved for integration: source shading and edge artifacts remain, and native-size faces lose eye definition. The hooded body has stray pale/green edge pixels; the headscarf knot loses definition.

Exact recolor palette checks fail: male 4 has 0/77 opaque pixels matching the seven required colors (69 distinct RGB colors); male 5 has 0/86 (72 distinct RGB colors). Female 2 also requires palette correction. Flat region colors and pixel cleanup are required before appearance.ts can recolor these bodies. No game integration or animation performed.
