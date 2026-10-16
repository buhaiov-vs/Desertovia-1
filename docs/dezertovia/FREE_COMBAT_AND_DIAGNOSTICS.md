# Owner request: no guard duty, manual combat, measured performance

16Oct2026: remove every duty/post/sector binding. Player chooses movement/target.
Work in fix/free-combat-and-diagnostics from LIVE0.7.3 plus docs-onlybe9c6c4.

1. Medically eligible citizens attack without a guard prerequisite. Existing paid
work is not reset. Explicit targets can be pursued anywhere reachable; no target
means remain at current position and attack only within range.
2. Ground clicks during combat are authoritative, including re-entry after retreat.
No automatic return to camp; retreat chooses local safe ground. HP/ammo/heal
cooldowns/receipts survive re-entry/waves.
3. Remove duty jobs from public catalog/UI, reject starting/renewing them. Keep
legacy definitions to settle already-paid cycles once via normal settlement.
Preserve wounds/medical pauses. Remove sector UI/counters/readiness dependency.
4. Confirmed hot path: battle packets trigger setVillage and all-citizen processing.
Separate combat sample updates from static-world work; retain topology/damage,
ward, civilian and fighter-membership updates. Interpolate overlapping snapshots
from current drawn position, not the previous future endpoint.
5. Low-cadence diagnostics during combat: simulation/backlog/path searches/nodes,
client FPS/frame/update time/packet gaps/reconnects. Bounded memory/rate/payload,
authenticated collection and owner report, settings export; no names/chat/tokens/
coordinates or per-frame server logs. Do not invent physical-phone FPS results.
6. Code first, then functional movement/attack/no-return/legacy settlement and
preservation tests; telemetry bounds/auth and renderer hot-path checks; exact CI.

No production synthetic waves/messages/payments/model probes or data resets.
Keep quarantine/Propose/budget. No replay of earlier deploy requests. Current
LIVE672d72e remains until fresh reviewed rollout. Single agent, no delegation.

## Implementation 0.7.4 (not installed)

Implemented manual orders/local-only self-defense for capable citizens and ruler,
no guard job/sector/counter/post movement. Battle movement and re-entry preserve
health, ammunition and healing receipts; idle final position is kept and published.
Retreat seeks nearby reachable safe ground, not camp. Historical definitions/JSON
fields remain solely for existing receipts and backward-compatible saved data.
RetiredGuardWorker settles one already-paid fixed-price duty through normal
settlement, never restarts it; medically paused work waits for normal recovery.
Other paid work stays intact. API fixtures disable this startup worker because
they inject historical cycles directly into SQL; explicit retirement tests run it.

Two concrete movement issues corrected: sub-cell navigation no longer snaps a
slow actor back to the previous tile centre, and overlapping fighter snapshots
start from current displayed position. Battle-only renderer updates skip static
work and all-citizen retargeting; topology and fighter membership still refresh.

Server: max32 battle histories,120 samples each; 15-second log summaries with
simulation/backlog/A* searches/nodes/blocked paths, recent lock/save/send timings,
process cumulative CPU/memory/GC. Stage values refer to latest completed stage,
not end-to-end network latency or a measured GPU frame. Owner-only read endpoint
/api/villages/{slug}/combat-diagnostics. No schema migration. Restart clears buffer.
Client: max120 windows,2048 frame/update samples per window; actual ticker elapsed
frame time, p95/max/slow frames, world update time, packet gaps/reconnects, modal
and target-FPS context. Stops outside the map. Settings download combines local
measurements and owner backend data; residents export local only. No automatic
client telemetry upload, personal data, chat, tokens or per-frame console logs.

Local verification:424 domain tests PASS;22 focused browser tests PASS on
desktop and phone emulation, including100 battle packets with zero civilian
rebuilds, geometry/death/render-cache/ward checks and interpolation continuity.
TypeScript/lint PASS (existing lint warnings). Initial full API run:236PASS,
3 failures: two old guard-requirement expectations updated to new owner policy;
one fixture SQL write raced startup retirement. Explicit retirement tests stay
active; the shared fixture now disables the startup worker. Fresh239API run
in progress. Not READY until required checks are complete.
No claim of physical phone performance before captured live measurements.
Before any installation: exact CI, fresh reviewed rollout against LIVE672d72e,
and add exact new game SHA→0.7.4 to bot feedback version registry with its CI.
Do not reuse post073 request/operator pins or change quarantine/Propose/budget.
