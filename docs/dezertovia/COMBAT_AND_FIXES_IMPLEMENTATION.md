# D-069 — реализация новой обороны и связанных исправлений

Поручение: весь код и проверки D-068 разрешены. Ветка feat/live-village-combat,
исходныйmain2e8ee2623464822ac9834489149d5e122a5eaf87. LIVE0.6.1 пока неизменён.
Установка не раньше16.10.2026 07:00 Europe/Oslo (05:00UTC), только после готовности
и точного успешногоCI. User authorization сохраняется после сжатия контекста.

| Блок | Статус |
| --- | --- |
| COMBAT-01 | implemented: отдельные серверные акторы, геометрия, адаптивный состав |
| COMBAT-UI | implemented: HP, выбор/автоатака, позиция/перехват |
| ENERGY-02 | implemented: проекция/обновление без перезахода |
| UI-05 | implemented: слои глаза/карточек/чата |
| CYCLE-02 | implemented: settled-цикл и медицинская пауза |
| ENERGY-03 | implemented: общая цена, конкурентное списание |
| LAND-01 | implemented: пересечение всего участка UI/API |
| STORAGE-UX | implemented: компактная сводка без смены ёмкостей |
| AI-SPEECH-01 | implemented: отдельный речевой контракт, guards до обрезки, фильтр старой истории |
| Checks | final exact CI required; previous source CI passed |
| Scheduled rollout | Automation confirmed for16.10.2026 07:00 Europe/Oslo; only ready exact CI; no switch before05:00UTC |

План: COMBAT_AND_FIXES_PATCH_PLAN.md + RAID_REWORK_PLAN.md.
Записывать реальные файлы/решения/числа/проверки/точныеSHA; не считать план результатом.

## Checkpoint1 — in progress, not tested/not ready to deploy

Added Domain RaidBattle/units/fighters, RaidNavigation, RaidBalance/RaidSimulation,
persistent palisade Fortification + repair compatibility; API preparation/dispatch/
orders/gate and EF mappings started. No migration/build/tests yet. Source needs
review and completion, not a deployable artifact. Next: close lifecycle/cancellation/
wave overlap; bounded delta networking; client actor selection/cards; energy/cycle
repros/fixes, storage and boundary preview; AI speech JSON screenshot fix.

Additional user steering: screenshot shows service JSON option/reason published as
Father speech. Confirmed competing JSON+plain text instructions in bot almighty.py.
Include AI-SPEECH-01: separate speech prompt and defense at API publication, preserve
quiet-hour privacy. No actual messages need deletion or synthetic test messages.
Scheduled task is registered; do not claim deployment completed before it runs.

## Checkpoint2 — candidate0.7, common verification in progress

Main code batch is implemented. New persistent RaidBattles and immutable bounded
SignalR snapshots; individual HP/cooldowns, one-side warning, formations, navigation,
open/closed gates, repairable breaches, minority outbuilding targeting, central
rally, bounded plunder, actual escaped-carrier stock removal, individual wounds,
retreat and one-time rewards. Outages freeze combat; later waves wait for earlier
waves and retain fighters' HP. Old pending engine0 events use legacy resolution.
The single director sees catalog/readiness and chooses side plus swarm/elite/mixed
under Propose; budgets freeze at announcement. Guard roles/captains/barracks and
existing armory are retained. Captain bonus acts within5tiles. No per-click DPS
or extra stamina charge beyond guard duty.

Frontend renders actual actors/HP/selection/attack/hit/death states, target orders,
hold/intercept, gate controls and active raid status. Energy projection/quotes
share one calculation, 30sec authoritative refresh and foreground refresh;
older citizen timestamps/battle revisions do not overwrite newer state. Cycle
startup ignores a stale end timestamp once already settled. Native wounds share
the existing medical work pause, resuming once without a second charge. Private
footprints use Overlaps on server/client. Existing storage capacity unchanged;
collapsible cards and availability/upgrade summary added. Eye z40, absent in full
chat. Service JSON speech guarded in bot and domain/API before truncation.

Additive migration20261015212416_LiveVillageCombat created; only adds battle table
and village fortification JSON. No data reset. No installed schema/runtime change.
Version candidate0.7.0, visible0.7, in-app notes dated16October. Tests and balance
simulations follow the completed batch; do not mark ready before their results.
Bot companion: /workspace/TG-bots-director-chat, fix/director-chat-protocol based
619c622 canonical. Separate speech prompt, same director/facts/privacy/budgets;
10 focused tests passed, full suite running. Only almighty.py changed at runtime.

First Domain run:365pass/1failed of366; failed carrier fixture still used Warning
which intentionally changes Waiting actors to Approaching. Fixture corrected to
Fighting, common verification continues. Do not claim366pass yet.

## Checkpoint3 — local verification / companion integration

372Domain tests PASS, including6clock-driven full encounters:77–142simulated
seconds, both repaired-defense wins and raids with bounded damage. First failed
carrier fixture fixed (Warning vs active battle), not hidden. Production frontend
build PASS; EF reports no pending model changes. API full first pass210/213;
2test assumptions updated for new engine/JSON parsing, native injury errorcode
compatibility fixed. Second API full run and118browser checks in progress.

Bot canonical updated concurrently to34ef8ff with deployed AI-budget repair
3fd0c1c. Merged canonical into speech branch (3f7038c); preserved background budget
accounting and backoff, resolved only documentation conflicts.11focused tests
PASS after merge; full exact CI38811556338 on PR38 is running. Earlier799pass
was before canonical integration, not claimed as merged full verification.
At release use ONLY almighty.py based on new budget baseline; never old0.6.1
app/town_proxy/llm payload. Private ops are being prepared with UTC05:00timegate;
no live-combat-request.json until authorized time, so no early runner install.

## Checkpoint4 — collision parity and final review fixes

Public PR37, initial runtime9c615c4; CI38812040529 backend/frontend/Docker pass,
browser still running at checkpoint (superseded by upcoming final source).
LocalAPI213PASS. Bot PR38 merged canonical0054f4c; exact tested/payload source
3f7038c886e8ea37c98191a32ba0c3f558b05e69, CI38811556338 SUCCESS:816 tests and
runner/client checks. Bot narrow payload must preserve deployed3fd0c1c budget
baseline, not old6e87f60. No installation done.

Final review fixes not yet committed: select whole enemy body including head;
HP bars above scaled sprites; enemy card interrupts quarter chat and yields to
other cards/dialogs. Legacy bad NPC service lines filtered from public history
without deleting DB rows or censoring player JSON. Model attempt pacing avoids
retrying one unanswered call every5sec; new speech/history focusedAPI checks7PASS.
Partial boundary API assertion added.

Important geometry finding: old new-nav checked river/ravine but client-only
ponds/ridges could be crossed. Added AuthoredTerrain.cs as collision port of
UNCHANGED worldMap.buildLayout, preserving all random draws. Eight independent
browser-generated golden fixtures (86400tiles) match server masks/hash exactly.
Domain380PASS after terrain navigation integration. Rope bridges remain passable;
no map drawing/seed/asset changes. Added actual path/water assertions to simulation
(to run on final source). New terrain code/masks do not require another migration.

Need next: final type/build + targeted updated browser checks and Domain/API as
needed; commit final source, full exact CI on PR37 (supersedes9c615c4). No release
readiness claim until finalCI. Initial local118browser suite was for9c615c4 and may
be stopped once superseded; do not claim it tests the later source.

Private ops prepared locally /workspace/bolig-almighty-release:
live_combat_release.py (hard05:00UTCtimegate), test_live_combat_release.py(3PASS),
new workflow triggering ONLY live-combat-request.json. That trigger file does NOT
exist; do NOT create/push before07:00Oslo. Prepare candidate JSON/payload/docs now,
with exact final game/bot SHA+CI receipt, readytrue only after finalCI. Current
private branch remainsa0dcc1e, changes not yet committed/pushed. OLDgame61d726c;
one bot filealmighty.py; new private record live-combat-0.7. Rollback withheld if
new pending battle state exists (old engine would resolve it incorrectly).
Automation already scheduled, must update its prompt with final manifest/PR/SHA
and exact future activation/check instructions once ready. Automation id is in
session tool history, never publish it to user. Server untouched until deadline.

## Checkpoint5 — final source follow-up before exact CI

Initial9c615c4 CI38812040529 completed SUCCESS:372Domain+213API+
117browser/1expectedskip; frontend/DockerPASS. Local initial browser run was
superseded/interrupted after68passed and a phone180sec timeout under software
rendering load; do not call it a full local pass. Remote exact initial source
completed the full browser suite successfully. Later source requires freshCI.

Terrain golden parity all8cases PASS;380Domain tests. Added actual movement/water
assertions, real wall repair destination, and safe auto-night skip if all approaches
blocked. Broken gear now included in defender reports. Native speech history guard
keeps invalid legacy DB records without displaying them, preserves player JSON,
and attempts obey pacing even when the model is silent. Seven targeted chat/API
checks PASS, plus real PostgreSQL parallel tick/reload/history-trim/one-time reward
integration PASS. Final frontend/source checks and full new exactCI next.

No server changes. All0.7/runtime ready flags remainfalse until the newCI finishes.

Final review addition: a palisade completed during the warning is initialized on
the first combat tick; frozen enemy budget/composition do not increase. Covered
by the PostgreSQL tick/reload test.8cbfc84CI is superseded by this source follow-up.


## Checkpoint6 — serial waves and final verification candidate

Found and fixed a queue-order defect during final review: deferring wave two
changes its actual StartsAt/raid.StrikesAt, which could put overdue wave three
first. Added immutable ScheduledAt to the NOT YET INSTALLED LiveVillageCombat
migration/model; no production migration edited. Active battle has priority;
otherwise original ScheduledAt/wave/id determines the next battle. At most one
live battle advances per village tick. Deferred flag changes are committed and
broadcast even without a movement step. Banner prioritizes active combat over
old deadlines of queued waves. Added real PostgreSQL three-wave regression,
including persistent fighter HP between waves, and a browser banner regression.

Large director snapshot (150 citizens, 20 maximum-length chat lines, prior
proposal history/intent plus all direction/profile options) fits the existing
36,000-character bridge limit. No external model calls used. Attempt pacing also
rechecks the global gap for each village, preventing a failed call in one village
from bypassing limits via the next village in the same worker sweep.

Simulation fixtures now use a deterministic village map seed AND regenerate its
wilds, then clear only optional obstacles/ravine; actual authored ponds/ridges
remain and every actor position is checked against server terrain collision.
Final source verification is in progress; do not install based on earlier CI.


Checkpoint6 local results:380Domain PASS on final deterministic geometry fixtures;
production frontend build PASS; EF no pending model changes. Three-wave API
regression PASS (fixture takes the same PostgreSQL advisory transaction lock as
background workers, and reloads the persisted microsecond-precision timestamp).
Earlier focused batch8other API checks PASS. Prior source848f087 CI38814781348
completed SUCCESS (380Domain/215API/browser/builds). New exact source CI next;
prior run is not substituted for it. Private operator suite14PASS including
UTC gate, CI pin guards and pending-battle-safe rollback behavior.
