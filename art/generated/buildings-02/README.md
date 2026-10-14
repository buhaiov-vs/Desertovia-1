# World buildings, second batch

17 buildings from section 2 of docs/sprites.md, completing the first generated pass of all 22 world buildings. Built-in image_gen used the Tiny Town and Tiny Farm sheets as style references. Exact prompts are recorded in prompts.json.

Each selected asset has its source PNG, a native-size PNG and an 8× nearest-neighbor preview. All 17 native canvas dimensions were verified against the row prompts, including rectangular canvases. Exports crop excess space using the solid-alpha bounds and fit with transparent margins without stretching.

These assets await final style and alpha-edge review. The drying rack includes an unwanted halo. An edit attempt is preserved as building-drying-rack-v2-source.png, with its prompt in repair-prompt.json, but was not selected because the halo persists. The complete set still needs palette simplification and colored fringe cleanup. No game integration has been performed.
