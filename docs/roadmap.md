# Desertod — Roadmap

Status as of 2026-10-13, 23:59 (dungeon #1 added: both builds succeed, 298 domain tests and 138 integration tests pass; production readiness check and deployment to the Hetzner server on 2026-10-13). Design documents: [game-design.md](game-design.md), [streamer-mode.md](streamer-mode.md), [cooperative-building.md](cooperative-building.md).

## How to use this file

- Tick an item (`- [x]`) when it is merged and works, not when it is started.
- Add a short note after the item if it was done differently from the plan, and update the design document it belongs to.
- New work goes under the phase it belongs to. If something blocks launch, add it to Phase 0.
- Update the date at the top when you change this file.

## Done

- Game rules (`Desertod.Domain`): stats, roles, experience curve, energy, challenge multiplier, streak, morale, hunger, shelter, expeditions and rare finds, storage buildings with slots, carried goods. Covered by domain tests.
- Cooperative building: blueprints placed anywhere on free camp land, sequential stages with parallel haul and labor jobs, crew requirement with "Lend a hand", repeat cost × 1.15, walking time by distance.
- Wild start: a campfire and no storage (the Heap is built first), stats at 0, name-only character creation, seven starting tasks.
- Buildable buildings: Heap, Lean-to, Wood pile, Stone pile, Drying rack, Hut, Woodyard, Stone yard, Granary.
- Tribe needs (quests): hunger, cold, storage room; Renown split among contributors.
- Discoveries (technologies): 16 discoveries in Camp, Village and Town eras; the Tsar or a Governor calls a vote and citizens choose, qualified citizens research at the discovery's building with a crew requirement; discoveries unlock Farm, Bakery, Workshop, Mine, Smithy, Library, Tavern, Stage and Storehouse, roles and yield bonuses. New early tasks: Tell stories, Study plants, Explore the land. See [cooperative-building.md](cooperative-building.md), section 5.
- Wildlife: rabbit, deer and bird herds per village that shrink when hunted and grow back over time; Hunt rabbits, Hunt birds (feathers), Hunt deer (Hunter). See [cooperative-building.md](cooperative-building.md), section 8.
- Farming: 3 beds per Farm; sow turnips, carrots, cabbage, flax or rye (better seeds need more Endurance), water the beds (anyone), harvest; Farmer title. Bakery (Baking discovery): Bakers bake grain into food. See [cooperative-building.md](cooperative-building.md), Farming.
- Backend: ASP.NET Core, PostgreSQL, SignalR live updates, background cycle settlement, concurrency retries, translatable error codes.
- Login: Twitch, Google (YouTube), Telegram, email and password (rate limited, PBKDF2 hashing, accounts per address limit), name-only login in Development.
- Frontend: React + PixiJS village view, minimap, task list, build panel, sites, needs, inventory, character sheet, first-steps guide, notifications.
- Public and private villages with invite codes, public village list.
- English and Russian, typed dictionaries.
- Server bootstrap: hardened `deploy/cloud-init.yml`.

## Phase 0 — Stabilize (launch blockers)

Goal: a buildable, deployable application.

- [ ] Initialize git and push to a remote repository. Git is initialized with one commit; no remote yet, and most of the work since (about 190 changed files) is not committed.
- [x] Fix the frontend build (`npm run build` in `web`).
  - [x] `web/src/i18n/ru.ts`: missing Russian keys.
  - [x] `web/src/pages/HomePage.tsx`: `api.createVillage` takes a `CreateVillageRequest`.
  - [x] `web/src/ui/CreateCitizenForm.tsx`: invite code argument.
  - [ ] Verify the public/private village user interface in a browser (create form, invite code entry).
- [x] `Dockerfile` for the application (frontend build into `wwwroot`, then the .NET publish). Also builds a migration bundle image. Both images build.
- [x] Production `docker-compose` with the application, PostgreSQL and a reverse proxy with automatic HTTPS (`deploy/docker-compose.yml`, `deploy/Caddyfile`). Running on the server since 2026-10-13 at https://91-98-8-14.sslip.io, deployed by copying the working folder into `/opt/desertod`.
- [x] Forwarded headers (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`; the application is reachable only through Caddy).
- [x] Persist data protection keys: derived from `DATA_PROTECTION_KEY`; production refuses to start without it.
- [x] Apply database migrations in production. The application runs `MigrateAsync` at startup.
- [x] Health check endpoint (`/healthz`, includes the database, reachable only as `localhost`), and a Docker health check for the `api` service (`dotnet Desertod.Api.dll --health-check`). Docker Compose only marks the container unhealthy; it does not restart it.
- [ ] Register the Twitch, Google and Telegram login applications with the production domain. Until then only email and password login is available.
- [ ] Password reset (email and password accounts have no way to recover a forgotten password).
- [ ] Nightly database backups stored off the server. Nightly dumps on the server disk (`postgres-backup` service, kept 30 days) are done and are included in the Hetzner server backups; a copy outside Hetzner is not.
- [ ] Rate limiting on task start, job start, village creation and hub connections. Done for the password endpoints only.
- [ ] Privacy policy and terms page.
- [ ] Continuous integration: `dotnet test` and `npm run build` on every push.
- [x] Confirm `DESERTOD_ENVIRONMENT=Production` in the server's `deploy/.env` (Development enables name-only login as anyone).

## Phase 1 — Closed beta

Goal: one or two friendly streamers playing for real.

- [x] Make Workshop buildable (stages and jobs), so planks exist. Needs the Carpentry discovery.
- [x] Make Smithy buildable, so iron ingots exist. Needs the Smelting discovery.
- [x] Make Farm buildable. Needs the Agriculture discovery.
- [ ] Start a first monument in every village (today new villages never get an active project), with materials adjusted to the wild start.
- [ ] Error tracking and structured log collection.
- [x] Application programming interface integration tests against a real PostgreSQL (`tests/Desertod.Api.Tests`, Testcontainers).
- [ ] Play-test stream with 5–10 people; record balance findings here.
- [ ] Tune crew sizes, hunger, energy and job amounts from the play-test.

## Phase 2 — Ages

Goal: long-term goals. See [cooperative-building.md](cooperative-building.md), section 5.

- [x] Age system. Done differently: eras come from discoveries that citizens vote for and research at buildings, not from building counts.
- [x] Camp age and Village age buildings with stages.
- [ ] Balance discovery costs and crews from a play-test.
- [ ] Better houses (House, Manor) and house upgrades.
- [ ] Palisade built per wall segment.
- [x] Roles unlocked by their building existing. Done differently: roles are unlocked by their discovery.

## Phase 3 — Streamer tools

Goal: more for the Tsar to do on stream. See [streamer-mode.md](streamer-mode.md).

- [ ] Decrees: Festival, Bell call, Tax relief, Call to the site.
- [ ] Construction site priority order.
- [ ] Storage orders (accept, refuse, empty).
- [ ] Cancel a started site with its materials left as a pile.
## Phase 4 — Deeper cooperation

Goal: "we need more people". See [cooperative-building.md](cooperative-building.md), sections 3 and 4.

- [ ] Job places per job and per workplace.
- [ ] Heavy jobs (need two citizens in overlapping cycles).
- [ ] Group hunting bonus.
- [x] Arrows made from feathers (Fletch arrows, Archery), shot by bows in fights.
- [ ] Personal share cap per stage.

## Phase 5 — Subscriber integration

Goal: subscriptions become citizenship. See [game-design.md](game-design.md), section 9.

- [ ] Twitch subscription check per village.
- [ ] YouTube channel membership check (YouTube Data application programming interface).
- [ ] Dormant citizens when a subscription lapses; wake up on resubscribe.
- [ ] Cosmetics, titles and house decorations paid with Renown.

## Phase 6 — Later ages and events

Goal: an endless list of goals.

- [ ] Town and Stronghold ages: Stone yard upgrades, Treasury, Market, Barracks. (Storehouse, Tavern, Stage, Library and Mine are buildable behind discoveries.)
- [ ] Roads and haul distance.
- [x] Raids and defense: beast ambushes on hunts and expeditions, night raids on the camp, undead events called by the Tsar, Keep watch, Patrol (Guard) and Command (Captain), a shared armory (spears, bows and arrows, swords, leather armor, shields) that wears out, wounds healed by remedies or poultices, damaged buildings and repairs, Barracks. See [game-design.md](game-design.md), section 12.
- [x] Dungeon #1, the Dead Lands: the Tsar opens it, a 60-second gathering with a class picker (classes from the gear in storage and the citizen's crafts), the march to the edge of the land, a live fight on a separate map (`/v/:slug/dungeon/:runId`) against 7 kinds of undead with aggro ranges, shared rewards per kill, wounds for knocked-out heroes, a 15-minute limit; the Tsar fights as the Voivode. See [game-design.md](game-design.md), section 13.
- [ ] Hand-drawn sprites for the Dead Lands (listed as New in [sprites.md](sprites.md), section 23).
- [ ] Balance dungeon foes, classes and rewards from a play-test.
- [ ] More dungeons.
- [ ] Balance enemy power, raid chance and gear from a play-test.
- [ ] Hand-drawn sprites for the enemies (they are drawn in code in `web/src/game/enemies.ts`).
- [ ] Trade at the Market (Merchant).
- [ ] Diplomacy between villages (Envoy, Negotiate).
- [ ] Draft plans (Architect) and Lead a crew (Foreman).
- [ ] Bot detection by renewal timing, with a flag for the streamer.

## Open design questions

From [game-design.md](game-design.md), section 11. There is no single right answer; record the decision here and in the design document.

- [ ] Seasons: do villages reset periodically or persist forever? Decide before public launch, because changing it later means wiping or migrating player data.
- [ ] Endurance as a fifth stat: keep or drop?
- [ ] Respec: can citizens lower one stat to raise another?
- [ ] Injury risk for tasks like deep mining (needs Alchemist remedies)?
