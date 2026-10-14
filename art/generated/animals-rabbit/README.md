# Rabbit animation — draft

Three separate built-in image_gen calls: sitting, launching and landing. Tiny Town and Tiny Farm are style references; frames 2 and 3 additionally reference frame 1 to preserve the animal identity. Exact prompts in prompts.json. Sources and individual 16x16 exports retained, with enlarged previews. character-rabbit.png is the assembled 48x16 horizontal strip, frames left to right without gaps. prepare.ps1 exports with nearest-neighbor sampling; assemble.ps1 joins them without resampling.

Native review: all face right and have long ears and white tails. Distinct poses are present, but the independent bounding-box fit changes apparent body size across frames. Eye and leg details weaken, colored fringe pixels remain (especially beneath the launch pose), and the palette has too much shading. Body-length consistency, pixel cleanup and loop continuity require correction before game use. Transparent corners and export dimensions pass. No game integration.
