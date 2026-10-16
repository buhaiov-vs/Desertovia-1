#0.7.6 server contention: implemented, NOT LIVE

Owner reports0.7.5 starts smoothly then stutters toward end of real battle.
Actual uploaded0.7.5 diagnostic confirms exact374323f,89server samples,
41enemies→0,1fighter→10. Simulation median2.006ms,p956.664ms,max43.661ms.
Recorded lock wait median238ms,p951666ms,max3015ms;14samples exceed1second.
These per-tick stages can repeat the most recent observed wait; they are not
14independently counted waits. New operation aggregates count actual leases.
Client8windows/3274frames,58frames>50ms,updatep953ms,packet gapmax1534ms;
218static updates/264battle packets. No complete canvas freeze is proven.
Raw owner upload remains outside git. No assertion of server CPU saturation.

Code inspection: every combat click force-refreshed all active linked residents,
including HTTP hospital batches and potentially relationships, under the shared
village lock. Limit that forced check to the acting resident/royal account; retain
force=true, fresh injury/failure restrictions, and periodic village-wide medical
and relationship sync. Do not weaken clinical/dungeon checks or spend/reset items.

Work settlement held the same lock for up to10separately committed transactions.
Use1resident per lock lease; rewards and idempotency remain in the same individual
transactions. No changes to combat speed/damage/balance or client interpolation.
Add bounded, combat-only operation aggregates (internal method names, counts,
maximum/total wait/hold; max32keys per trace). Preserve old diagnostic fields.

Local focused tests and full exact CI pending. Fresh reviewed rollout required;
LIVE remains0.7.5. No oldrequest replay/private main/forced raids/player messages/
model probes/payments/progress resets. Backups/preservation/Propose/quarantine
remain mandatory; next ordinary real-phone battle still needed after release.

Red regression replay of old all-resident refresh +10resident lease:4of5new
tests fail as expected (wrong medical batch; one lease instead of3). Original
implementations restored after replay. Updated full API suite running.
