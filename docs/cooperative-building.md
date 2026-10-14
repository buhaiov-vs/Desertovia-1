# Desertod — Everything Is Built by Hand

This document extends [streamer-mode.md](streamer-mode.md). It replaces the instant storage builds of phase 1 and the fixed starting village.

## 1. Principles

1. **Nothing happens without a player.** The Tsar and the Governors only *plan*. Every log, stone, beam and roof tile on the map was moved by a citizen during one of their tasks of a few minutes. No timers, no automatic workers, no "the building finishes itself".
2. **Work is split as finely as possible.** A building is not one "Build" task. It is many small jobs: bring stone, bring wood, lay the foundation, raise posts, tie the frame, weave walls, thatch the roof. Different jobs suit different people.
3. **A building needs a crew, not a hero.** One very active player must not be able to finish a building alone. Every stage needs several *different* citizens.
4. **Everyone starts with nothing.** The tribe begins as wild people at a campfire: no buildings, no tools, all skills at 0. Everything else is built up over many streams.
5. **The goal is endless growth.** More storage, more workshops, more walls, bigger houses, then monuments. There is always a next building.

## 2. The Tsar and the Governors plan, citizens build

- The village owner (the streamer) is the **Tsar**. The Tsar never works: they plan, call votes, invite, call undead events and walk the map.
- The Tsar appoints **Governors** among the citizens: click a citizen on the map and tick "Governor"; untick to dismiss. A Governor stays a citizen who works and votes, and can also place and withdraw blueprints and call and cancel votes. "Governor" shows under their name on the map and as a badge in their top bar. Only the Tsar appoints, calls undead events and changes subtitles and the invite code.
- The Tsar or a Governor places a **blueprint** on the map (a free tile area, later road-connected). This costs nothing and moves nothing: it is a plan drawn on the ground.
- A blueprint becomes a **construction site** with stages (section 3). Citizens see its jobs in their task list; the Tsar sees the site visually changing: pegs → stone base → posts → walls → roof.
- The Tsar or a Governor may withdraw a blueprint nobody has worked on yet.
- (Proposed, not built: a blueprint placed by a Governor becomes a **work order** sent to the Tsar, who approves or rejects it. How approval should play is still open, so for now Governor blueprints take effect at once.) (Proposed, not built: reordering site priority, and cancelling a started site with its materials left as a pile for citizens to carry back.)
- Limits: at most 3 open construction sites plus 1 monument.

## 3. Construction: stages and jobs

A building's blueprint is a list of **stages** done in order. Each stage has several **jobs** that run in parallel.

| Job type | What the player does | Example jobs |
|---|---|---|
| Haul | Takes goods from storage (or their hands) to the site | Bring stones, Bring logs, Bring reeds |
| Prepare | Turns raw goods at the site into parts | Hew beams, Split stone, Bundle thatch, Mix clay |
| Raise | Labor that advances the stage | Clear the ground, Lay stones, Raise posts, Tie the frame, Weave walls, Thatch the roof |
| Plan | Skilled oversight (later ages) | Mark the outline, Measure, Supervise |

Example: **Hut** (first real house)

| Stage | Jobs (amount) | Crew |
|---|---|---|
| Ground | Clear the ground (3 cycles), Mark the outline (1) | 2 |
| Foundation | Bring stones (24 stone), Lay stones (4) | 3 |
| Frame | Bring logs (20 wood), Hew beams (4), Raise posts (3, heavy), Tie the frame (2) | 4 |
| Walls | Bring branches (12 wood), Mix clay (3), Weave and daub walls (4) | 3 |
| Roof | Bring reeds (16 reeds), Bundle thatch (3), Thatch the roof (4, heavy) | 3 |
| Finish | Hang the door (1) | 1 |

Rules:

- **Stages are sequential, jobs inside a stage are parallel.** Hauling for later stages may start early, so haulers always have something to do.
- **Job places:** each job has a maximum number of citizens at once (for example Lay stones: 2). Extra players must pick another job.
- **Crew requirement:** a stage completes only when at least *Crew* **different** citizens have contributed to it. To avoid deadlock on small streams:

  ```text
  Crew needed = min(stage crew, number of citizens active in the last 2 hours)
  ```

- **Heavy jobs** (Raise posts, Thatch the roof, Lift the bell) only progress when at least 2 citizens work on that job in overlapping cycles. Alone, the cycle gives experience but no progress, and the citizen is told "You need someone to lift with you."
- **Personal share cap:** one citizen may contribute at most 40% of any stage's labor (the existing monument rule was 20%; small buildings get a looser cap).
- Materials delivered to a site are drawn as piles next to it, so the Tsar can see a site waiting for stone.

## 4. The wild start

- **New tribe:** a campfire, trees, loose stones, a river, berry bushes, game animals. No storage, no Town Hall, no houses. The first storage is usually a **Heap** (storage for any good, 4 slots × 10, needs 4 sticks), built from goods in hand.
- **Characters:** no point allocation. Every stat starts at 0 and grows only by doing work. Character creation becomes just "choose your name".
- **First jobs** (no requirements):

  | Job | Gives | Note |
  |---|---|---|
  | Gather sticks | Wood | |
  | Pick up stones | Stone | |
  | Forage berries | Food | |
  | Cut reeds | Reeds | New good, for roofs |
  | Hunt | Meat (Food) and Hides | **Group hunt:** yield × (1 + 0.25 × other hunters in overlapping cycles), up to ×2 |
  | Tend the fire | Morale | The camp's only comfort at first |

- **Sleep:** everyone sleeps under the trees at first. Energy regeneration depends on shelter:

  | Shelter | Energy regeneration |
  |---|---|
  | Under the trees | × 0.8 |
  | Lean-to | × 0.9 |
  | Hut and better | × 1.0 |

- **Eating** (decision 3): each finished cycle eats 0.2 food, from the citizen's hands first, then from storage. With no food the cycle still gives yield but experience × 0.5 and energy regeneration × 0.5 ("You are hungry"). The Tsar sees thin, slow citizens, not a number.

## 5. Discoveries (technologies), eras and the endless building list

Every building unlocks new jobs, new buildings, or both. Later buildings, crafts and roles need a **discovery** first. Discoveries are grouped into **eras**; the village era is the latest era it discovered something of (Wild camp before the first one).

**How a discovery is made (as built):**

- The Tsar or a Governor **calls a vote**: ticks two or more available discoveries (every discovery they build on is known; one is enough when only one is available). The vote pops up on every screen. Each citizen has one vote and can change it while the vote is open. The vote closes once more than half of the present citizens have voted (away citizens, see game design 4.4, are not counted), and the results pop up for everyone. The most voted discovery becomes the current research until it is discovered; ties go to the one with more progress, then catalog order. The Tsar or a Governor can cancel an open vote; the previous choice stays.
- Every discovery is **researched at a building**, like an upgrade in Warcraft. The Tsar and the Governors see which building the chosen discovery needs and plan it.
- Only citizens with the **required skills** may research (for example Intelligence 5, or Strength 4). A research cycle takes the cycle length plus the walk from the camp to the building; the researcher stands at that building on the map.
- Research work per cycle uses the same stat factor as a task (primary and secondary stat of the discovery). A discovery is found when its work is done **and** `min(crew, citizens active in the last 2 hours)` different citizens studied it, like a building stage.
- Early tasks build the skills research needs: **Tell stories by the fire** (Intelligence, Charisma, morale), **Study plants** (Intelligence, Dexterity, some berries), **Explore the land** (Intelligence, Endurance, a roam).

| Era | Discovery | Researched at | Needs first | Researchers need | Unlocks |
|---|---|---|---|---|---|
| Camp | Stone tools | Stone pile | — | Strength 2 | Gather wood and stones ×1.25 |
| Camp | Herb lore | Drying rack | — | Intelligence 2 | Forage and Study plants ×1.25 |
| Camp | Weaving | Lean-to | — | Dexterity 2 | Cut reeds ×1.25 |
| Camp | Tracking | Drying rack | — | Dexterity 2, Intelligence 1 | Hunting ×1.2 |
| Village | Agriculture | Granary | Herb lore | Endurance 3, Intelligence 2 | Farm |
| Village | Carpentry | Woodyard | Stone tools | Dexterity 3, Strength 2 | Workshop, Carpenter |
| Village | Mining | Stone yard | Stone tools | Strength 4 | Mine, Miner, Deep miner |
| Village | Writing | Hut | — | Intelligence 3, Charisma 2 | Library, Scholar |
| Village | Brewing | Granary | Agriculture | Charisma 3 | Tavern |
| Village | Baking | Granary | Agriculture | Dexterity 3, Intelligence 2 | Bakery (needs a Farm), Baker |
| Town | Smelting | Mine | Mining | Strength 5, Intelligence 3 | Smithy, Smith |
| Town | Music | Tavern | Brewing | Charisma 4, Dexterity 3 | Stage, Bard |
| Town | Bookkeeping | Library | Writing | Intelligence 5 | Storehouse, research ×1.25 |
| Town | Mechanics | Workshop | Carpentry, Smelting | Intelligence 5, Dexterity 5 | Artisan, Saw planks ×1.25 |
| Town | Alchemy | Library | Writing, Herb lore | Intelligence 6 | Alchemist |
| Town | Forging | Smithy | Smelting | Strength 6, Dexterity 4 | Master smith, Smelt iron ×1.25 |

Camp and Village discoveries ask for stats 2–4, reachable in the first one or two days of play; Town discoveries ask for stats 5–6. Researching a discovery is gated by these stats, not by a skill level; the challenge rule compares the researcher's primary stat with the highest one.

**Not built yet:** a Stronghold era (stone walls, towers, church, Treasury, Market, Barracks) and monuments behind discoveries.

- Each building type can be built many times. Repeats cost a little more each time (`cost × 1.15^count`), so expansion keeps going without becoming free.
- Walls are built **per segment**, which gives an endless, very cooperative job list for big streams.
- Professions (Miner, Smith, Carpenter, …) are unlocked by their discovery; their work needs a skill level, and skill levels need stats.

## 6. What changes in the current build

| Area | Today | With this design |
|---|---|---|
| Storage build | Instant, paid from storage | Blueprint → construction site with stages and jobs |
| Start | Town Hall village, stats 1 + 8 points | Campfire only, all stats 0, name only |
| "Build" task | One task per project | Many jobs per stage; crew and heavy-job rules |
| Carried goods | Unloaded by a 10-second timer | Unloaded only when that citizen acts (start, settle, or a "Carry to storage" job) |
| Projects | Fixed chain of monuments | Monuments are the last stage of the building list |
| Food | Only a resource | Eaten each cycle; hunger slows citizens |

Suggested order of implementation:

1. Construction sites with stages, jobs, job places, crew and personal cap; replace instant storage builds and the old "Build" and "Deliver" tasks.
2. Wild start: stats at 0, name-only creation, a campfire and no storage, first jobs, shelter and energy.
3. Heavy jobs and group hunting (overlapping-cycle cooperation).
4. Ages and the full building list, repeats, wall segments.
5. Eating and hunger.

## 7. Decisions taken

1. Existing villages were **reset** to the wild start.
2. Cooperation is enforced by the **crew requirement only**. Job places, heavy jobs and the personal share cap are not built.
3. Food is **eaten every cycle** (hunger).
4. Carried goods are unloaded **only by the player**, through the "Carry to storage" task. Every action takes time that depends on the walk between the places involved.

## 8. As built (steps 1 and 2, with hunger)

**Map and walking.** The camp, workplaces and buildings have tile coordinates (`WorldMap`). A walk costs 2 seconds per tile, one way, straight line.

| Action | Duration |
|---|---|
| Gathering task | 3 min + walk from the workplace to the storage that takes the good |
| Crafting, camp and project task | 5 min (+ walk to storage for crafted goods) |
| Expedition | 3 min + walk from the gate to the camp |
| Roam the land | 3 min; the walk is the task. 6 glances, each finds 2 wood, 1.5 stone, 1.5 food or 2 reeds (× yield factor) with 25% + 2.5% per Dexterity. Needs empty hands |
| Carry to storage | 15 s + walk from the camp to the storage |
| Haul job | 3 min + walk from where the good is picked up to the site yard |
| Labor job, research | 5 min + walk from the camp to the site |

**Blueprints.** Free to place anywhere on the camp land west of the river (snapped to whole tiles), as long as the 3×4 footprint stays off other buildings, the campfire and the project sites; at most 3 open sites, requirements checked (for example a Hut needs a Lean-to). A repeat needs `1.15^count` times the haul amounts. Only a site nobody has worked on can be withdrawn; materials are never refunded.

**Trees on a blueprint.** Trees do not stop a blueprint. Each tree standing on the footprint adds 3 work to a **Fell the trees** job in the first stage (Strength, Endurance; no material needed). Every unit of that work fells a third of a tree and brings its wood to storage (what does not fit stays in the builder's hands), so the full job yields 15 wood per tree. The trees stay drawn on the map until they fall, wood gatherers do not chop them, and a felled stump under a building never grows back.

**Jobs.**

- Haul jobs take the good from the citizen's hands first, then from storage, and may run for the current or any later stage.
- Labor jobs run only for the current stage. Labor that works material (lay stones, tie the frame, …) cannot run ahead of the material delivered; clearing, marking and "lend a hand" can.
- At stat 0 every job moves its listed amount per cycle (sticks 4, stones 3, reeds 5, hides 1, labor 1).

**Crew.** A stage finishes when its jobs are done **and** `min(stage crew, citizens active in the last 2 hours)` different citizens worked on it. When the jobs are done but hands are missing, a "Lend a hand" job opens for citizens who have not yet worked on that stage.

**Finished buildings.** Storage kinds join the storage list (the Heap, Wood pile, Stone pile, Drying rack, Woodyard, Stone yard, Granary). Others become structures. Shelters assign beds, oldest citizens first: under the trees ×0.8 regeneration, Lean-to (2 beds) ×0.9, Hut (4 beds) ×1.

**Wild start.** New villages get a campfire and no storage: gathered goods stay in hands (half experience, and the Storage quest opens) until the tribe builds its first storage, usually a Heap, from what it holds. Stats start at 0 and level 0 → 1 needs 100 experience. Creation is name only. First tasks: Gather wood, Gather stones, Forage berries, Cut reeds, Hunt rabbits (food and hides), Hunt birds (food and feathers), Tend the fire, Carry to storage. Tasks of later buildings (fields, mines, smithy, library, …) are locked with "Needs: building".

**Hunger.** Every settled cycle except carrying eats 0.2 food, from the citizen's hands first (a hunter whose catch did not fit in storage eats it), then from storage. A hungry citizen gets half experience and half energy regeneration. The Tsar sees no hunger hints.

**Taking and dropping.** Storage is shared by the whole tribe; there is no handing goods from one citizen to another. An idle citizen can take a good out of one storage building into their hands, instantly, until they hold 10 of it, for example to carry it into a better building or to a site. "Drop the load" throws away only the goods storage has no room for, so goods taken out of storage cannot be destroyed.

**Wildlife.** Every village has three herds stored on the server. Hunting takes animals out; a herd grows back toward its capacity on its own (half of the missing animals return every half-life).

Hunting is one "Hunt" card where the citizen picks the prey. Each prey needs a level of the Hunting skill (which itself rests on Dexterity and Endurance, see [game-design.md](game-design.md), section 3.1), and the profession is Laborer for all of them. Experience follows the hunting time (hunting time ÷ 3 min), so every prey gives the same experience per minute; the choice is about materials.

| Herd | Capacity | Half-life | Hunting ground | Task | Hunting | Hunting time | Experience | Animals per cycle (× yield factor) | Full catch |
|---|---|---|---|---|---|---|---|---|---|
| Rabbits | 60 | 45 min | Hunting grounds | Hunt rabbits (Dexterity / Endurance) | 0 | 3 min | ×1 | 2.5 | 5 food, 1 leather |
| Deer | 20 | 90 min | Deer woods | Hunt deer (Dexterity / Endurance) | 2 | 6 min | ×2 | 0.5 | 12 food, 4 leather |
| Birds | 60 | 30 min | Bird grove | Hunt birds (Dexterity / Intelligence) | 3 | 90 s | ×0.5 | 3 | 1 food, 4 feathers |

The walk from the hunting ground to storage is added to the hunting time. Hunting 3 earns the Hunter title.

- Abundance = animals ÷ (half of capacity), at most 1. Animals caught = animals per cycle × yield factor × abundance, never more than are left. The outputs scale by animals caught ÷ animals per cycle.
- A hunt cannot start when less than 1 animal is left. A cycle that catches nothing counts as overflow (half experience).
- Villages created before wildlife get full herds once.
- The Tsar sees the animals on the map (fewer when hunted out) and the herd size when clicking an animal. Citizens see the count on the task card.
- On the map, animals live all over the wild land (not in the camp, the monastery or water); deer keep to the woods, rabbits to the meadows. The hunting grounds in the table only set the walking time.
- A hunter on duty chases one animal of the kind for the prey's hunting time and always reaches it at the end, then carries it home for the rest of the cycle. Animals run from hunters on duty from 4 tiles away (from other citizens only at 1.5 tiles), circle back toward their home spot, and tire in the last 20 seconds of a chase. This is drawing only; the catch is decided by the server.
- Feathers go to the Storehouse. Nothing uses them yet (arrows are a later step).

**Farming.** Every built Farm has 3 beds stored on the server. A bed holds one crop that grows in real time; citizens sow, water and harvest it. Better seeds need a higher Farming skill (which rests on Endurance and Dexterity). The profession is Laborer for all farm tasks; Farming 3 earns the Farmer title. Sowing is one "Sow" card where the citizen picks the seeds, like the Hunt card.

| Seeds | Task | Farming | Grow time (watered) | Harvest |
|---|---|---|---|---|
| Turnips | Sow turnips | 0 | 1 h | 15 food |
| Carrots | Sow carrots | 2 | 1.5 h | 24 food |
| Cabbage | Plant cabbage | 3 | 2 h | 36 food |
| Flax | Sow flax | 4 | 2 h | 10 flax |
| Rye | Sow rye | 5 | 3 h | 20 grain, 10 food |

- Sow (Endurance / Intelligence): puts the seeds into the first empty bed. Cannot start when every bed is sown.
- Water the beds (Endurance / Strength, no skill needed): walks from the ford to the farm and waters up to 3 thirsty beds, driest first. A watered bed is wet for 30 minutes; a bed counts as thirsty when it is wet for less than 15 more minutes. Cannot start when no bed is thirsty.
- Harvest (Endurance / Dexterity): takes the ripest bed and carries the crop to storage (the walk is added). Output is the crop's harvest × yield factor; hands must have room for it. Cannot start when nothing is ripe.
- A wet bed grows at full speed, a dry bed at a quarter of it. Growth is computed from the time of the last change, like herds.
- When another citizen took the bed (or the water, or the ripe crop) first, the cycle counts as overflow (half experience).
- Flax goes to the Drying rack or Storehouse, grain to the Granary or Great Granary. Nothing uses flax yet.
- Anyone can bake once the village has the Baking discovery (Baking 3 earns the Baker title): they bake bread at the Bakery: 2 grain from storage become 8 food per cycle (Dexterity / Endurance). Bread is food: it goes to the food storages and feeds hungry citizens like any other food.
- On the map each bed is one row of the farm: darker soil while wet, and the plant grows through sprout, young and ripe pictures. The Farm popup lists each bed with its crop, growth and wetness.

**Not built yet:** job places, heavy jobs, personal share cap, group hunting, ages, walls and segments, priority ordering, refunds on cancel.

## 9. Tribe needs (quests) and first steps (as built)

A **need** opens by itself when the village has a problem and closes when the problem is solved. Both roles see it, split by what each one does:

| Need | Opens when | Tsar or Governor | Citizens | Reward |
|---|---|---|---|---|
| The tribe is hungry | a cycle found no food in storage | Plan food storage (Drying rack or Granary) | Build it; store food: 3 per citizen, 10 to 40 | 15 Renown, morale +3% |
| The tribe sleeps in the cold | fewer beds than active citizens | Plan shelter (Lean-to or Hut) | Build beds for everyone | 20 Renown, morale +3% |
| No room for *good* | a cycle's goods did not fit in storage | Plan storage that takes the good | Make 10 units of room (build it, carry the good to a site) | 10 Renown, morale +1% |

- At most one open need per kind (and per good for storage). Needs are stored; Renown is split between the citizens who did useful cycles while the need was open (one share per cycle).
- Useful cycles: hunger — food that went into storage, or work on a food storage site; cold — work on a shelter site; storage — carrying that good to storage or to a site, or work on a site of storage that takes it.
- The Tsar sees the citizens' objectives too, worded as the citizens' work with the progress numbers after them ("Citizens fill it with food (12 / 30)").
- Citizens get a "Tell the Tsar" chat line while the planning part is not done. Governors see the planning objectives worded as their own work and a Plan button instead.

**First steps guide.** A short step-by-step card per role, detected from play and remembered in the browser: citizens — join, first task, renew in time, look at the needs, help build; Tsar — first blueprint, copy the invite link, look at the needs. It can be skipped.
