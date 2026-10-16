#0.7.4: free combat and measured performance

Status: INSTALLED AND INDEPENDENTLY VERIFIED at2026-10-16T20:04:47.4389681Z.
Preflight38958939581, install38959075262 and separatecheck38959583714 SUCCESS.
Tested game
74adbe8c1d778f0751a755a4b36dbefe6f16a405, CI38956420909 SUCCESS424Domain+239API+
151browser/1normal skip, frontend lint/build/DockerPASS. PublicPR48 mergeddfdc0ac;
merge tree identical to tested source. Bot metadata67f91498f843fea5148a8f34ee9ca6be2e3c0ea0,
CI38957570632 SUCCESS887tests/serverSKIPPED; canonicalPR46 merged6ae84ce.
20fresh rollout guard testsPASS. Preflight38958939581 SUCCESS on old672d72e,
119profiles, exact bot/protected hashes and owner routing. Install38959075262 SUCCESS; separatecheck38959583714 SUCCESS.

## Resulting behavior

- No guard duties, defense sectors, camp leash or guard counter. A capable player
  chooses reachable ground or an enemy. A selected enemy is pursued; without a
  target the player stands and attacks only within weapon range.
- Ruler and ordinary citizens can attack. Clinical/dungeon restrictions stay.
  Retreat seeks nearby safe ground; re-entry keeps actor health/ammo/heal receipts.
- Old paid guard cycles settle once through ordinary receipts. Other paid work and
  paused recovery are preserved. Legacy definitions/JSON fields remain only for
  compatible storage/receipts; they no longer control live movement or permission.
- Slow units no longer bounce to the previous tile centre. Overlapping movement
  snapshots interpolate from displayed position. Combat-only updates skip static
  world work and civilian retargeting; geometry and fighter membership still refresh.

## Diagnostics

Server automatically writes bounded Combat performance summaries every15seconds
of actual combat: simulation/backlog, path search/nodes, recent lock/save/send,
cumulative processCPU/memory/GC. In-memory max32battles*120ticks, cleared at restart.
Owner-only endpoint: /api/villages/{slug}/combat-diagnostics.

Client automatically buffers max120windows of frame/update/world/packet/reconnect
timings. Settings→Скачать диагностику боя exports them; owner's export adds server
history. The buffer stops on leaving the map. No new map panel, per-frame server
logs, automatic client uploads, chat/names/player IDs/positions/tokens in reports.
Frame times must be read with targetFPS/modal/visibility; they do not directly time
GPU rendering. Packet gaps alone are not proof of server CPU saturation.

Local424Domain+239API+22focusedbrowser PASS; Settings export verified at390/1280px
with a synthetic mocked backend response.100battle snapshots caused zero civilian
updates in the actual map renderer while topology/membership changes still applied.
Workspace synthetic119residents/48enemies/60ticks: mean18.2ms, max92.1ms without
palisade; mean19.4ms,max183.4ms with closed palisade. This is not a live-server or
physical-phone FPS measurement and does not prove every source of lag eliminated.

Actual receipt: RELEASE_0_7_4_RESULT.json.119→119profiles,0progress losses,
121accounts/loginbindings,25migrations,24publicJS chunks and feedback versionPASS.
Old unfinished duties0;14protected bot modules/configs/quarantine/Propose/budget
preserved. PostgreSQL backup restore catalog verified, SQLite/config/code/profile
snapshots saved privately. No natural postdeployment raid sample existed at final
check; diagnostics are enabled and wait for ordinary play. Do not reinstall.
Backups/profiles/quarantine/Propose/budget are required; no forced live raid, model,
message/payment probe, data reset or blind engine downgrade is permitted.
