# Desertod — Story Quests

This document extends [cooperative-building.md](cooperative-building.md). The tribe already gets **need quests** (hunger, cold, full storage) that open by themselves when something is wrong. **Story quests** are the other half: a fixed chain of village goals that gives the tribe something to work towards, from the first fire to a bell for the monastery across the river.

## 1. Rules shared by every story quest

- **One story quest at a time.** The next one opens when the previous one is completed. Need quests keep running beside it.
- **Village-wide.** Every citizen works towards the same quest. The Tsar sees the goal and its progress numbers (as with need quests).
- **Objectives are read from the village**, not ticked by hand: a building exists, a project is finished, food is in storage. Counted objectives (for example "tend the fire 5 times") count the cycles of every citizen together.
- **Credit and reward** work like need quests: every useful cycle while the quest is open is recorded, and on completion the Renown pool is split by those cycles. The village gets morale.
- **Monument quests start a project.** Opening the quest places the monument's project on the map; the existing project rules apply (material delivery, labor, the 20% personal contribution cap, project Renown on completion). Projects no longer start one after another by themselves: only a quest starts one, so at most one project runs at a time.
- **Engineer supervision** is a property of the project. Small works (statue, pier, palisade) need none; great works (bridge, bell tower, Great Granary) need an Engineer to survey the site and supervise the builders.
- **Materials of the first ages only.** Projects ask for goods the tribe can produce at that point (wood, stone, reeds, food, gold), so no story quest is blocked by a later age.
- Quest texts (title, story line, objectives) are translated in English and Russian on the client; the server sends identifiers only.



## 2. The chain


| #   | Quest                     | Opens after   | Objectives                                 | Reward (Renown pool, morale) |
| --- | ------------------------- | ------------- | ------------------------------------------ | ---------------------------- |
| 1   | Keep the fire burning     | village start | Tend the fire 5 times                      | 10, +2%                      |
| 2   | A roof over our heads     | 1             | A Lean-to stands                           | 15, +3%                      |
| 3   | Stores for the cold       | 2             | 20 food in storage                         | 15, +3%                      |
| 4   | The first house           | 3             | A Hut stands                               | 25, +5%                      |
| 5   | The Tsar's monument       | 4             | Project "Tsar's monument" finished         | 40, +8%                      |
| 6   | A fishing spot            | 5             | Project "Fishing pier" finished, 5 catches | 30, +4%                      |
| 7   | A palisade against wolves | 6             | Project "Palisade" finished                | 50, +8%                      |
| 8   | The Tsar Bridge           | 7             | Project "Tsar Bridge" finished             | 80, +10%                     |
| 9   | The monks' gold           | 8             | Dig the gold vein 20 times                 | 50, +5%                      |
| 10  | A bell for the monastery  | 9             | Project "Bell tower" finished              | 80, +15%                     |
| 11  | The Great Granary         | 10            | Project "Great Granary" finished           | 60, +5%                      |


After quest 11 the chain is finished; need quests and free building continue forever.

## 3. Quest by quest



### 1. Keep the fire burning (Тёплый очаг)

- **Story:** "The night is cold and the fire is small. Feed it, or the tribe will freeze before it builds anything."
- **Objective (citizens):** finish 5 cycles of *Tend the fire* (any citizens together).
- **Credit:** each finished *Tend the fire* cycle.
- **Map:** the campfire burns brighter while the quest is open (no new art needed).
- **Why first:** it is the one job everyone can do from the start and teaches the cycle.



### 2. A roof over our heads (Крыша над головой)

- **Story:** "Trees are a poor roof when it rains. Poles and reeds will do better."
- **Objectives:** Tsar or a Governor: place a Lean-to blueprint. Citizens: the Lean-to is built.
- **Credit:** any work on a Lean-to site, and gathering wood or reeds while the quest is open.



### 3. Stores for the cold (Запасы на холода)

- **Story:** "Berries rot, meat spoils. Fill the stores before the frost."
- **Objective (citizens):** 20 food in storage at the same time.
- **Credit:** food that reaches storage; work on a Drying rack.
- **Hint for the Tsar and the Governors:** a Drying rack keeps food.



### 4. The first house (Первый дом)

- **Story:** "A lean-to keeps off the wind. A house keeps a family."
- **Objectives:** Tsar or a Governor: place a Hut blueprint. Citizens: the Hut is built.
- **Credit:** any work on a Hut site.



### 5. The Tsar's monument (Памятник царю)

- **Story:** "A messenger from the Tsar has passed by. A village that honours the Tsar will be remembered by him."
- **Project "Tsar's monument":** a stone statue on a pedestal by the campfire (tile 20, 9).
  - Materials: 60 stone, 30 wood. Labor: 12 cycles. No survey, no Engineer.
  - Visible stages: pegs → stone pedestal (materials) → rough block (half the labor) → finished statue.
- **Objective:** the project is finished.
- **Credit:** the project's deliveries and labor.



### 6. A fishing spot (Рыбное место)

- **Story:** "The river is full of fish, and the tribe stands on the bank with empty hands."
- **Project "Fishing pier":** a wooden pier from the west bank (tile 29, 16) three planks into the river.
  - Materials: 40 wood, 20 reeds. Labor: 8 cycles. No survey, no Engineer.
  - Visible stages: poles in the water, replaced by planks from the bank outwards as labor is done.
- **Unlocks:** the task *Fish* (laborer, Dexterity and Endurance, 3 food per cycle at stat 0, workplace: the pier).
- **Objectives:** the pier is finished; 5 *Fish* cycles.
- **Credit:** each *Fish* cycle. Pier work already earns the project's own completion reward, so it is not credited twice.



### 7. A palisade against wolves (Частокол)

- **Story:** "Wolves howled at the edge of the camp last night. A wall of sharpened logs will keep them out."
- **Project "Palisade":** a fence of stakes around the camp land (tiles 0…27 × 0…25) on the west, north and south; the river is the east wall, and the west side stays open at the gate.
  - Materials: 200 wood. Labor: 25 cycles. No survey, no Engineer. Completion reward: 60 Renown, +3% morale.
  - Visible stages: pegs mark the line; the fence grows piece by piece from the gate, north and along the top, then south and along the bottom.
- **Credit:** project work (deliveries and building).
- **Objective:** the project is finished.



### 8. The Tsar Bridge (Царский мост)

- **Story:** "The monks across the river have waited a long time. A bridge worthy of the Tsar will join both banks."
- **Project "Tsar Bridge":** the existing bridge project with first-age materials.
  - Materials: 240 wood, 100 stone, 60 reeds (for rope). Survey: 10 cycles. Labor: 60 cycles. **Engineer required** (survey and supervision).
  - Visible stages: survey posts across the river, then deck spans with railings laid one by one from the west bank.
- **Objective:** the project is finished.
- **Credit:** project work (deliveries, survey, building) and supervision cycles that let builders work.
- **Unlocks:** the far bank: the road to the monastery and the monks' gold.
- **Training an Engineer:** Engineer needs Intelligence 4 and Strength 2. The laborer task *Measure the river* (workplace: the ford by the road, tile 29, 14.5; main stat Intelligence, side stat Strength; 0.25 knowledge per cycle) is open from the start, so one citizen qualifies within one day of energy (under 36 cycles, under two hours of play). Measuring is not credited to the quest.
- **Pacing rule (from the user):** one real day of energy gives about one profession level and about two stat points, so a player has something new to learn every day; tasks last a few minutes so players make several short visits a day (see `game-design.md`, sections 5.2 and 5.3).



### 9. The monks' gold (Монастырское золото)

- **Story:** "The old monks speak of a gold vein in the hill behind the monastery. Now the bridge stands, the tribe can reach it."
- **Gold vein:** a workplace on the far bank (tile 40, 17), visible from the start as a stony outcrop (tiles 39…41 × 16…17) with gold nuggets.
- **Unlocks:** the task *Dig the gold vein* (`dig-gold`; laborer, Strength and Endurance, 1 gold per cycle at stat 0, workplace: the gold vein). It needs the finished Tsar Bridge (`RequiredProject`). It is a different task from the late *Mine gold* of the deep miner.
- **Depletion:** the vein holds 150 gold for the whole village, counted in `Village.GoldVeinMined` and sent to clients as `goldVeinRemaining`. The last cycle gets only what is left; after that the server refuses the task (`task.veinEmpty`), the task card is locked and the nuggets on the outcrop turn grey one by one as the vein empties.
- **Objective (citizens):** 20 *Dig the gold vein* cycles. The design first asked for 30 gold in storage, but gold in storage is also spent (the bell needs 20), so counting cycles is clearer.
- **Credit:** each *Dig the gold vein* cycle that got gold (a cycle on an empty vein is not credited).
- **Done.**



### 10. A bell for the monastery (Колокол для монастыря)

- **Story:** "The monastery has a church but no bell. A bell would ring for both banks."
- **Project "Bell tower":** a free-standing tower with a gold dome behind the monastery (tiles 41…43 × 1…4).
  - Materials: 150 stone, 80 wood, 20 gold (new task *Deliver gold*, 2 per cycle). Survey: 8 cycles. Labor: 40 cycles. **Engineer required.**
  - Visible stages: corner posts, then the tower rises row by row; the gold dome appears when it is finished.
- **Credit:** project work and supervision, as for the bridge.
- **Objective:** the project is finished.
- **Reward extra:** the elder rings in the morning: morale +15%.



### 11. The Great Granary (Великая житница)

- **Story:** "With more mouths to feed, the tribe needs a store that never runs empty."
- **Project "Great Granary":** the existing granary project with first-age materials.
  - Materials: 200 wood, 150 stone, 40 reeds. Survey: 5 cycles. Labor: 40 cycles. **Engineer required.**
  - Site: tiles 25…28 × 22…24 in the south of the camp, as before.
- **Credit:** project work and supervision, as for the bridge.
- **Unlocks:** the Great Granary storage (as today).
- **Objective:** the project is finished.



## 4. Changes to existing systems


| Area              | Today                                         | With story quests                                                                            |
| ----------------- | --------------------------------------------- | -------------------------------------------------------------------------------------------- |
| Project start     | Next catalog project starts when one finishes | A story quest starts its project; nothing starts by itself                                   |
| Engineer          | Every project needs survey and supervision    | Per project: statue, pier and palisade need none                                             |
| Project materials | Planks, iron, chains                          | First-age goods only (wood, stone, reeds, gold)                                              |
| Tasks             | Unlocked by buildings                         | Also unlocked by a finished project (*Fish*) or a quest (*Dig for gold*)                     |
| Quests            | Need quests only                              | Need quests plus one story quest                                                             |
| Map               | Monastery is scenery                          | Statue, pier, palisade, bridge, gold vein and bell tower appear and grow with their projects |




## 5. Order of implementation

Each quest is built, tested and checked in the browser before the next one:

1. Story-quest framework (kinds, chain, objectives, credit, rewards, client panel) with quest 1. **Done:** `StoryQuests.cs`; the chain grows as each quest is implemented.
2. Quests 2–4 (buildings and food: no new projects). **Done.**
3. Quest 5 with project start by quest and optional Engineer. **Done:** the statue stands at tile 16, 9 (between plots 1 and 2), not 20, 9.
4. Quest 6 with project-unlocked tasks. **Done:** tasks can require a finished project (`TaskDefinition.RequiredProject`).
5. Quest 7 with the palisade drawing. **Done:** the fence is drawn only; it does not block walking.
6. Quest 8 with first-age bridge materials. **Done:** with the *Measure the river* task and the faster experience curve.
7. Quest 9 with the gold vein and its depletion. **Done:** migration `GoldVein` adds the `GoldVeinMined` village column.
8. Quests 10 and 11. **Done** (ahead of quest 9).

