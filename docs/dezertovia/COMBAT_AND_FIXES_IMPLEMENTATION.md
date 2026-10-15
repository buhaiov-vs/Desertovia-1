# D-069 — реализация новой обороны и связанных исправлений

Поручение: весь код и проверки D-068 разрешены. Ветка feat/live-village-combat,
исходныйmain2e8ee2623464822ac9834489149d5e122a5eaf87. LIVE0.6.1 пока неизменён.
Установка не раньше16.10.2026 07:00 Europe/Oslo (05:00UTC), только после готовности
и точного успешногоCI. User authorization сохраняется после сжатия контекста.

| Блок | Статус |
| --- | --- |
| COMBAT-01 | in_progress: аудит геометрии и серверного цикла |
| COMBAT-UI | pending |
| ENERGY-02 | pending |
| UI-05 | pending |
| CYCLE-02 | pending |
| ENERGY-03 | pending |
| LAND-01 | pending |
| STORAGE-UX | pending |
| Checks | after code package |
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
