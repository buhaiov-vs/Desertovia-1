# Desertod — The Streamer as City Planner

Inspired by *Zeus: Master of Olympus*. The streamer plans and manages the city; the citizens (subscribers) are the people who live and work in it. This document extends [game-design.md](game-design.md) and replaces its single-number storage rule (section 5.6).

The decisions in section 10 are settled. Phase 1 (storage buildings, carried goods) is implemented; section 11 lists where the build differs from the full design. The original "hidden numbers" rule was dropped on 2026-10-12 (see section 2).

## 1. Core idea

| | Streamer (Tsar) | Citizens (subscribers) |
|---|---|---|
| Does | Places buildings, roads and storage; sets storage orders; issues decrees; chooses the next monument | Gather, craft, haul, build; pick their own jobs every few minutes |
| Never does | Gathers or carries anything | Places buildings or decides the layout |
| Knows | The city and its **numbers**: stock, capacity, what each building takes and how much was delivered | The same numbers, plus why their own job is blocked |
| Learns about problems | From the numbers, the map and the chat of the streaming platform | Directly from the game |

The fun is the loop between the two: citizens hit a wall, they complain in chat ("the woodyard is full, build another one!"), the streamer reacts on stream. The game deliberately does **not** pass messages from citizens to the streamer.

## 2. Information rules

**Decision (2026-10-12):** the Tsar sees the numbers. The earlier rule "the Tsar has no ledger" (resource and capacity numbers, material progress and shortage speech bubbles hidden from the owner's page) is dropped: planning without knowing what a building takes or what storage holds was more frustrating than fun.

The streamer's view (the owner's game page):

- Storage amounts and capacity in the top bar and the inventory.
- What every building takes before it is placed (repeats cost more), and per-material and per-job progress of every construction site and the village project. Clicking a site, the project, a farm or an animal herd on the map shows its progress.
- Need and story quest progress with numbers.
- Citizen speech bubbles about full storage and hunger.

The Tsar still never works, so the actions stay with the citizens. The citizen's view keeps everything it has (inventory with numbers, error messages), plus:

- A clear blocked state: "Woodyard is full — your 3 wood stays in your hands."
- A **"Tell the Tsar" helper** that copies a ready-made chat line to the clipboard, for example `Woodyard full 200/200 — we need another woodyard!`. The player pastes it into Twitch, YouTube or Telegram chat themselves. This helps players talk to the streamer without the game talking for them.

The village owner cannot have a citizen in their own village.

## 3. Storage

### 3.1 Storage buildings

There is no global stock any more. Every resource unit lives in a storage building, and each building accepts only certain goods.

| Building | Accepts | Slots × units per slot | Footprint | Cost |
|---|---|---|---|---|
| Town Hall (starting) | everything | 6 × 20 | 3×3 | given at start |
| Woodyard | Wood, Planks | 4 × 50 | 3×3 | 40 wood |
| Stone yard | Stone, Iron ore, Coal | 4 × 40 | 3×3 | 30 wood, 20 stone |
| Granary | Food | 4 × 60 | 3×3 | 60 wood, 20 stone |
| Storehouse | Iron, Leather, Chains, Mechanisms, Remedies | 8 × 15 | 3×3 | 40 wood, 30 stone, 10 planks |
| Treasury | Gold | 2 × 50 | 2×2 | 50 stone, 10 iron |
| Great Granary (Great Granary monument reward) | Food, Wood, Stone | 6 × 100 | 3×3 | not buildable |

Knowledge is not a good: it is kept on the village with no limit and needs no storage building.

- A **slot** holds one kind of good. A Woodyard with 4 slots can hold 200 wood, or 150 wood and 50 planks, but not 4 kinds of anything.
- Goods are drawn as stacks on the building (one stack sprite per 25% of a slot), so a filling storage building is visible on the map as well as in the numbers.
- Capacity for a resource = free space in all **connected** storage buildings that accept it (see section 5 for "connected").

### 3.2 Storage orders (streamer)

As in Zeus, the streamer sets an order per storage building and per good:

| Order | Effect |
|---|---|
| Accept (default) | Workers may unload this good here |
| Refuse | No new goods of this kind are unloaded here |
| Empty | Stored goods of this kind are moved out to other buildings by haulers (a laborer task "Haul goods") |

Orders are a planning tool, for example keeping a Woodyard next to the bridge reserved for planks.

### 3.3 Full storage: workers keep the load

When a citizen's cycle finishes:

```text
Produced = Yield (as in game-design.md section 6.1)
Stored   = min(Produced, free space for that good in connected storage)
Carried  = min(Produced − Stored, 10 − already in hands)   (stays in the citizen's hands)
Lost     = Produced − Stored − Carried
```

- Hands hold at most **10 of one good**, the same limit as taking goods out of storage. Whatever fits neither in storage nor in hands is lost; the task report shows it as "+2(0) food (storage and hands are full)".
- A citizen **cannot start a task when every good it produces has full hands and full storage** ("Your hands are full of wood and storage has no room for it."). They are free to take any other job.
- Carried goods unload automatically as soon as space appears (the server retries every settlement tick). The citizen walks to the storage building on the map and stands there holding the load, so a crowd of woodcutters around a full Woodyard is visible to the streamer.
- Carried goods can be used directly: *Deliver wood* to a construction site takes from the citizen's hands first. This gives players something useful to do while waiting for a new Woodyard.
- A citizen can **drop the load** (the goods are lost) to make room in their hands.

Experience for the blocked cycle:

```text
StoredShare = Stored / Produced
Experience  × (0.5 + 0.5 × StoredShare)
```

This keeps the old Overflow rule (×0.5 when fully blocked) and still rewards the work that did fit.

## 4. Workplaces and jobs

Tasks are no longer always available. A task can be done only if the streamer has built its workplace, and each workplace has a number of **job places**.

| Workplace | Tasks | Job places | Placement rule | Cost |
|---|---|---|---|---|
| Woodcutter's hut | Chop wood, Hunt | 6 | Next to forest | 20 wood |
| Quarry | Quarry stone | 6 | Next to rock | 30 wood |
| Mine | Mine coal, Mine iron ore, Mine gold | 4 | On an ore vein | 40 wood, 20 planks |
| Farm | Tend the fields, Forage berries | 8 | On grassland | 30 wood |
| Smithy | Smelt iron, Forge chains | 3 | Anywhere | 40 stone, 20 wood |
| Workshop | Saw planks, Build mechanisms | 4 | Anywhere | 40 wood |
| Tavern | Chat at the tavern | 6 | Anywhere | 40 wood, 10 food |
| Stage | Play the gusli | 2 | Anywhere | 20 planks |
| Library | Read the chronicles, Research, Draft plans, Brew remedies | 6 | Anywhere | 40 wood, 20 planks |
| Market | Trade, Negotiate | 4 | Anywhere | 40 wood, 20 stone |
| Barracks | Patrol, Command | 6 | Anywhere | 60 stone, 20 iron |
| City gate | Go on an expedition, Long expedition | 8 | Map edge | given at start |

- Job places fill first come, first served. A citizen who renews within the grace window keeps their place.
- When a workplace is full, the task shows "No free job places" to the citizen and nothing to the streamer, except a visible crowd around the building.
- Building a second hut adds more job places. This is the streamer's main lever for steering the economy.
- Skill level requirements from game-design.md still apply. The workplace is an additional requirement.
- Starting village: Town Hall, City gate, one Woodcutter's hut, one Quarry, one Farm.

## 5. Roads and distance

- Buildings must touch a road to work. A workplace or storage building without a road connection is drawn with a "no road" sign and does nothing. This is visible to the streamer because it is a placement problem, not a stock problem.
- **Connected** storage = reachable by road from the workplace.
- Distance matters a little, so that a sensible layout pays off:

```text
HaulTiles   = road distance from workplace to the nearest accepting storage building
HaulFactor  = max(0.7, 1 − 0.01 × max(0, HaulTiles − 10))
Yield      ×= HaulFactor
```

Up to 10 tiles is free; at 40 tiles or more yield drops to 70%.

## 6. Building and construction

- The streamer opens **Build mode** (owner only), picks a building and places it on the tile grid. Valid tiles glow, invalid ones turn red; roads are drawn by dragging.
- Placing a building creates a **construction site**, which reuses the existing project system (game-design.md section 7): materials must be delivered by citizens and labor must be done. Small buildings skip the Survey phase. The Engineer supervision rule applies only to large buildings and monuments.
- **Monuments** (Tsar Bridge, Bell Tower, later churches and the monastery) are the big co-operative goals and keep survey, supervision and the 20% contribution cap. As in Zeus, monuments are what the city is remembered for.
- Several construction sites can be open at once. The streamer sets their priority order. Citizens see the priority as "Tsar's priority" next to delivery tasks, but may still choose freely.
- Demolishing returns 50% of the materials to connected storage; goods stored inside a demolished storage building are moved out if there is space, otherwise lost.
- To stop the streamer from flooding the map with sites that nobody can finish, at most **3 construction sites plus 1 monument** can be open at once.

## 7. Housing

- Houses give **energy regeneration**, not permission to play (subscribers must always be able to play).

```text
HousedCitizens = sum of house capacity (Hut 4, House 6, Manor 10)
Homeless       = citizens beyond housing capacity, newest joiners first
Energy regeneration × 0.75 for homeless citizens
```

- House levels follow Zeus: a house upgrades when it has food (Granary in reach) and an entertainment building (Tavern or Stage) nearby. Upgraded houses hold more people. This gives the streamer a reason to plan neighborhoods.

## 8. Decrees (streamer)

Decrees are the streamer's direct, on-stream actions. Each one costs goods and has a cooldown, so it is a decision, not a button to spam.

| Decree | Cost | Effect | Cooldown |
|---|---|---|---|
| Festival | 50 food | Morale +0.1 for 2 hours | 12 hours |
| Tax relief | 20 gold | Energy regeneration +25% for all citizens for 6 hours | 24 hours |
| Call to the site | — | Construction yield +20% on the top priority site for 30 minutes | 3 hours |

If the goods for a decree are missing from storage, the decree fails with "The council could not gather enough".

## 9. What changes in the current build

| Area | Today | With this design |
|---|---|---|
| Storage | One amount and one capacity (500) per resource on the village | Storage buildings with slots; capacity derived from connected buildings |
| Overflow | Yield lost, experience × 0.5 | Goods carried by the citizen; same-good tasks blocked; experience × (0.5 + 0.5 × StoredShare) |
| Tasks | Always available if stats allow | Also need a built, road-connected workplace with a free job place |
| Projects | Fixed catalog chain (bridge → granary → bell tower) | Streamer-placed construction sites + monuments chosen by the streamer |
| Streamer page | Same as citizen page | Build mode, storage orders, decrees, priorities; numbers shown |

Suggested order of implementation:

1. **Storage buildings and carried goods.** This alone delivers the requested loop: workers stuck with wood, citizens complaining in chat, the streamer reacting. Building placement can be temporary (a fixed list of storage spots the streamer unlocks) before the full Build mode exists.
2. **Build mode** with grid placement, construction sites, demolish, priorities.
3. **Workplaces with job places.**
4. **Roads and distance, storage orders, housing, decrees.**

## 10. Decisions

1. **How much the streamer sees:** everything citizens see, numbers included (changed on 2026-10-12; it was visual only).
2. **Full storage:** workers keep up to 10 of a good in their hands; the rest is lost.
3. **Owner as a citizen:** the streamer cannot be a citizen in their own village.
4. **Job places per workplace:** yes (Zeus style), in phase 3.
5. **Roads and distance:** in a later phase.

## 11. Phase 1 as built

> Superseded in part by [cooperative-building.md](cooperative-building.md): buildings are no longer instant, the Town Hall start was replaced by a bare campfire, and the automatic unload was removed.

- Every storage building counts as connected (no roads yet).
- Goods are stored in buildings in this order: specialized buildings first, then buildings that take any good. Goods are taken from buildings that take any good first.
- Storage orders (section 3.2) are not implemented yet.
