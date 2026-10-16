#0.7.6: combat server contention

INSTALLED AND INDEPENDENTLY VERIFIED at2026-10-16T21:38:28.7746706Z.
Preflight38970195603, install38970339155 and independentcheck38970711893 SUCCESS.
Exactgame d0f88e2e92d83e8c30a7566fb21c916a51023b1d, CI38969100973 SUCCESS:
424Domain+244API+155browser/1normal skip, frontend lint/build/DockerPASS.
PublicPR50 mergee62f516250890bd401fc8ce373b5f0a9f38cab7b tree equals tested source.
Botregistry891d1a839bed94de648d5abcf6d9176878cc89b1 /CI38969173093 SUCCESS887tests,
serverSKIPPED; canonicalPR48 merge14e6dd5b2bb5f20410c0fdb09ab6c3b9f0c51d35
has identical tree. Only version registry metadata changed; civic byte-identical.
Local244API testsPASS,20operator guardsPASS,4readonly parser testsPASS.

## Evidence and resulting behavior

Owner's exact0.7.5 report: early smooth, late stutter. Simulation median2.006ms,
p956.664ms,maximum43.661ms; recorded lock wait maximum3015ms. Stage values can
repeat the most recent observed wait; per-tick sample counts are not independent
wait events. Client updatep953ms, most windows~29fps at30target,58frames>50ms
across3274frames; packet gapmax1534ms. This is not proof of server CPU saturation.
Raw reports remain private. Server queue delays and remaining physical-phone
motion must be distinguished; a new natural battle capture is still required.

Every combat click previously forced clinical refresh for all linked residents
inside the shared village lock. Now force-refresh only the acting resident or
royal account. Fresh injury or unavailable clinical authority still blocks the
order. Periodic village-wide medical/relationship synchronization remains.
Work settlement now releases the village lock between residents; each reward
continues to use its own existing transaction and idempotency path. No paid work
or clinical recovery is reset. Combat speed/damage/balance/client interpolation
are unchanged in this patch.

Diagnostics add bounded per-operation counts, maximum/total wait and hold times,
using internal method names only (max32keys per battle trace). Holders and waiters
can now be distinguished. Old timing fields remain; no player IDs/names/messages
or credentials are added. Read-only private parser accepts only bounded timing
fields and internal method labels, never raw logs.

Regression replay of old all-resident refresh/ten-resident lease fails4of5new
cases as expected. Fixed full API244testsPASS locally and in exactCI, including
fresh injury/bridge-failure denial, royal account without Citizen row, one lease
per resident, existing wages/clinical/progress behavior and bounded diagnostics.

## Preservation

119profiles before→119after,0progresslosses;121accounts/loginbindings preserved.
25migrations,24publicJS chunks and feedback live-version lookupPASS. PostgreSQL
backup restore catalog verified; SQLite/code/config/profile backups saved
privately.14protected botmodules/configs/quarantine/Propose/budget retained.
No forced raids/messages/model probes/payments/resets. No natural postdeploy
combat sample at finalcheck, so live phone resolution is not asserted. See
RELEASE_0_7_6_RESULT.json. Do not reinstall/replay older rollout requests.
