# Building icons

All 22 section-3 building icons from docs/sprites.md have been generated with the built-in image_gen tool. The full prompt for each asset is recorded in prompts.json; Tiny Town and Tiny Farm were supplied as visual references.

Each icon has its generated source, a 16x16 transparent PNG, and an 8x nearest-neighbor preview. All 22 exported dimensions and transparent top-left corners were verified. prepare.ps1 reproduces missing exports without overwriting existing files.

Status: generated, awaiting final style audit. Native-size silhouettes have been inspected, but shading and edge colors still require palette cleanup to match the Kenney references. No game integration has been performed.
