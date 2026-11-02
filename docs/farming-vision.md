# Farming — Feature Vision

Status: vision draft v0.2, 2026-11-02. Decisions on rotting, seeds and theft accepted (section 9); other questions open (section 10). Numbers are examples for tuning, not decisions.

Inspiration: "Весёлый фермер" (Happy Farmer), the VKontakte farm game. It is a copy of the Chinese QQ "Happy Farm" from 2008. The mechanics listed below are from memory, not checked against the game today.

## 1. Problem statement

Farming today is a short loop: sow one of five crops into 3 beds, water every 30 minutes, harvest. A bed never fails, never needs anything but water, and the Farmer title stops at Journeyman (Farming 3) with no Master title. Citizens who like quiet, steady work have little to master, and a Farm adds nothing that the village talks about. Happy Farmer kept millions of people coming back several times a day with almost the same loop, because the beds *needed* them, friends could help or hurt, and the farm grew visibly.

## 2. What Happy Farmer did, and what we take

| Happy Farmer mechanic | What it gave players | Desertovia adaptation | Verdict |
|---|---|---|---|
| Plots of land, crops grow in real time | A reason to come back | Already built: Farm beds | Keep |
| Seed shop for coins | Choice and progress | Seeds come from the harvest (seed stock), better seeds need Farming level | Adapt |
| Weeds and bugs appear on crops | Small chores, lower yield if ignored | Bed troubles: weeds, pests, crows. Each is a short task for anyone | Adapt |
| Friends remove weeds and bugs for experience | Helping others feels good | Every citizen helps the village beds; helpers get experience and a share of Renown | Adapt |
| Friends plant weeds and bugs on you | Mischief | Dropped: citizens share one village, sabotage hurts everyone | Drop |
| Stealing ripe crops from friends | Urgency to harvest, social drama | Wildlife and raiders steal unguarded ripe crops; scarecrow and guards protect | Adapt |
| Guard dog | Protection to buy | Scarecrow building, Guarding citizens, a farm dog later | Adapt |
| Crops wither if not harvested | Urgency | Overripe crop: yield falls after a ripe window, then rots | Adapt |
| Several harvests from one planting | Long-term planning | Perennials: berry bushes, orchard trees | Adapt |
| Fertilizer skips a growth stage | Speed-up, often paid | Manure and compost from livestock and kitchen waste; never bought with money | Adapt |
| Land expansion and better soil (red, black) | Visible growth | More beds per Farm level, ploughing and soil fertility | Adapt |
| Player level and experience | Progress | Farming skill, Farmer and new Master title | Keep |
| Decorations | Self-expression | Renown cosmetics for the Farm (fence, scarecrow looks) | Later |
| Ranch with animals (added later in the genre) | Second loop | Barn with livestock fed by crops | Adapt, later phase |

## 3. Goals

1. **Farming is a real profession.** Farming has work at levels 0, 3 and 7, and a Master title. Target: at least 10% of active citizens hold the Farmer title 30 days after release.
2. **Beds need care, not only water.** A bed that nobody looks after yields clearly less (about half) than one that is cared for. Target: average yield factor of cared-for beds at least 1.4 times that of neglected ones.
3. **Small helpful tasks for everyone.** Any citizen, even at Farming 0, can do a useful farm chore in under 3 minutes. Target: at least 30% of active citizens do one farm chore per day.
4. **Farming feeds the village economy.** Crops feed bakers, livestock, weavers (flax) and kitchens. Target: farming gives 40–60% of the village's food in the Village era.
5. **The streamer sees the farm live.** Ripe, dry and troubled beds are visible on the map, so chat can react ("the crows are at the cabbage!").

## 4. Non-goals

- **Paying real money to speed up crops.** Speed-ups only come from in-game work (manure, skill). Paid speed-ups go against a fair community game.
- **Citizens sabotaging the village farm.** Planting weeds on others was fun between strangers; inside one village it only punishes the group.
- **Stealing between villages.** Rejected for now (section 9, decision 3). Would need trading and travel between villages first.
- **A free-placement farm editor.** Beds stay rows of the Farm building on the map. Free placement is a separate map project.
- **Seasons and weather affecting crops.** Weatherlore exists, but seasons are a separate world feature. The design leaves room for it (section 7, P2).

## 5. Users

| Who | What they want |
|---|---|
| Farming citizen (main player of this feature) | A craft to master, steady work, visible results |
| Casual citizen (any skill) | Quick, useful chores between other tasks |
| Guard or hunter | A reason to protect the fields |
| Tsar or Governor | Plan how many farms, fields and barns the village needs |
| Streamer and chat | Watch the farm, react to crows, raids and record harvests |

## 6. User stories

**Farming citizen**

- As a farming citizen, I want better crops at higher Farming levels so that my skill matters.
- As a farming citizen, I want a Master title at Farming 7 so that my long work is seen by everyone.
- As a farming citizen, I want to plough a bed to raise its fertility so that the next crops yield more.
- As a farming citizen, I want to save seeds from my harvest so that the village can keep sowing its best crops.
- As a farming citizen, I want to plant berry bushes or fruit trees that give several harvests so that I can plan for days, not hours.
- As a farming citizen, I want to spread manure on a bed so that it grows faster.

**Casual citizen**

- As a casual citizen, I want to pull weeds or chase crows in one short cycle so that I help without learning farming.
- As a casual citizen, I want to see on the task card which beds have trouble so that I do not walk there for nothing.
- As a casual citizen, I want to be told (in the page and in Telegram, if I opt in) when beds are ripe or under attack so that I can come and help.

**Guard or hunter**

- As a guard, I want to watch the fields during the night so that wildlife and raiders cannot steal the ripe crops.
- As a hunter, I want pests like hares and crows to be hunted near the fields so that the hunt protects the harvest.

**Tsar or Governor**

- As the Tsar, I want to plan more Farms, a Scarecrow and a Barn so that the village food grows with the population.
- As the Tsar, I want to see the farm summary (beds, crops, troubles, seed stock) so that I can decide what to build next.

**Streamer and chat**

- As a streamer, I want crop troubles and a stolen harvest to be visible on the map and in the chat so that my viewers have something to react to.

**Edge cases**

- As a citizen, when another citizen fixed the trouble before me, I want my cycle to count as overflow (half experience, as today) and not as a failure.
- As a citizen, when the seed stock for a crop is empty, I want to be told why I cannot sow it and which crop still has seeds.
- As a citizen, when a crop rots, I want it to leave compost so that the loss is not total.

## 7. Requirements

### P0 — must have (Phase 1: the bed care loop)

**P0.1 Bed troubles.** While a crop grows, a bed can get one trouble at a time: weeds, pests or crows. A bed with a trouble grows at half speed and loses part of its harvest per hour (example: −10% per hour, at most −50%).

- [ ] A growing bed gets a trouble at random, on average once per crop growth (tuning).
- [ ] New task cards: Pull weeds, Pick pests, Chase crows. Farming 0, short cycle, need a bed with that trouble.
- [ ] The trouble is fixed at the end of the cycle; a second citizen for the same trouble gets overflow.
- [ ] The map shows the trouble on the bed row; the Farm popup lists it.
- [ ] A bed without trouble behaves exactly as today.

**P0.2 Ripe window and rotting.** A ripe crop keeps its full harvest for a ripe window (example: as long as its grow time), then loses yield, then rots.

- Given a bed became ripe 1 hour ago and the window is 1 hour
- When the next hour passes without a harvest
- Then the harvest falls step by step to half, and after that the bed rots and becomes empty with compost left behind.

**P0.3 Farming levels up to 7 and a Master title.** Add the Master farmer title at Farming 7 and work for levels 6–7.

- [ ] Master title (name to decide, section 9) at Farming 7.
- [ ] At least one level-7 task, for example "Breed seeds" (turns seeds into better seeds) or a level-7 crop.
- [ ] The professions table in [game-design.md](game-design.md) shows the new title.

**P0.4 Clear task cards.** Every farm card says why it cannot start: no empty bed, no thirsty bed, nothing ripe, no trouble, no seeds.

### P1 — nice to have (Phase 2: soil, seeds and protection)

**P1.1 Seed stock.** Harvest gives seeds as well as the crop (example: 10% of the harvest). Sowing uses seeds from the Storehouse. Turnip seeds are always free, so a village can never be stuck.

**P1.2 Plough and fertility.** Each bed has fertility (example: 0.8 to 1.3). Ploughing raises it; every harvest lowers it a bit. Compost and manure raise it. Fertility multiplies the harvest. "Plough the field" is already named in the skills table.

**P1.3 Scarecrow and night watch.** Ripe crops at night can be stolen by wildlife unless a Scarecrow stands at the farm or a guard is on duty. Raiders who win a raid can take ripe crops as part of their loot.

**P1.4 Telegram reminders.** Opt-in message when the village has ripe beds about to rot, or a trouble nobody fixed for 30 minutes. At most one message per citizen per hour.

**P1.5 Farm upgrade.** A Farm level 2 adds beds (3 → 5). Needs a discovery and building materials.

### P2 — future (design must leave room)

- **Barn and livestock.** Chickens (eggs), sheep (wool), cows (milk), fed with crops. Manure from the Barn feeds P1.2. Farm animal sprites already exist (`craftpix-net-291971`).
- **Orchard and berry bushes.** Perennials with several harvests and a long first growth. Crop sprites exist (`craftpix-net-200380`).
- **Seasons.** Crops prefer seasons; Weatherlore warns of frost or drought.
- **Farm cosmetics for Renown.** Fences, scarecrow looks, bed borders.
- **Fairs between villages.** Show the best crop, trade seeds.

Design notes for P2: keep the bed state open for more fields (fertility, trouble, crop age); let a crop have more than one harvest; let a building other than Farm hold "beds" (Orchard, Barn pens).

## 8. Success metrics

| Kind | Metric | Target | Check at |
|---|---|---|---|
| Leading | Citizens doing at least one farm chore per day ÷ active citizens | 30% | 1 week |
| Leading | Troubles fixed within 30 minutes | 70% | 1 week |
| Leading | Ripe beds harvested before they lose yield | 80% | 1 week |
| Lagging | Citizens with the Farmer title ÷ active citizens | 10% | 30 days |
| Lagging | First Master farmer in a village | within 10 days of steady play | 30 days |
| Lagging | Share of village food from farming, Village era | 40–60% | 30 days |
| Lagging | Rotted crops ÷ all crops | under 10% | 30 days |

Measurement: from the server's cycle records. Whether those records can answer these questions today is an open question.

## 9. Decisions

Accepted by the owner on 2026-11-02.

| # | Question | Decision | Rejected options |
|---|---|---|---|
| 1 | How harsh is rotting? | After the ripe window the yield falls step by step to half, then the crop rots and leaves compost. | Never rots, only falls to half; rots at once after the window |
| 2 | Where do seeds come from? | Seed stock saved from the harvest; turnip seeds are always free. | Seeds stay free as today; seeds bought with a currency |
| 3 | Who steals ripe crops? | Wildlife at night and raiders after a lost raid. | Only raiders; other villages too |

## 10. Open questions

| # | Question | Who | Blocking |
|---|---|---|---|
| 4 | Name of the Master title: Master farmer, Agronomist, Elder farmer? Russian name? | Owner | Yes for P0.3 |
| 5 | Should farm chores get the Challenge multiplier rule (section 5.4 of game-design) so that pulling weeds is not an easy experience farm? | Owner, design | Yes for P0.1 |
| 6 | Can the server's cycle records give the metrics in section 8? | Engineering | No |
| 7 | How often should troubles appear so the farm feels alive but not like a chore wall? Needs playtesting. | Design | No |
| 8 | Do troubles happen while nobody in the village is online? (Recommended: no, to not punish quiet hours.) | Owner | Yes for P0.1 |

## 11. Phasing

| Phase | Content | Depends on |
|---|---|---|
| 1 | Bed troubles, ripe window and rotting, Master title, clear cards (P0) | Nothing new |
| 2 | Seed stock, plough and fertility, Scarecrow and night watch, Telegram reminders, Farm level 2 (P1) | Raids (built), Telegram (built) |
| 3 | Barn and livestock, manure | A new discovery, animal sprites |
| 4 | Orchard, seasons, cosmetics, fairs | Seasons and inter-village features |
