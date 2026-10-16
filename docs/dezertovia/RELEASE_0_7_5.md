#0.7.5: combat motion fix

INSTALLED AND INDEPENDENTLY VERIFIED at2026-10-16T20:59:15.5558416Z.
Preflight38965661962, install38965752003 and independentcheck38966145677 SUCCESS.
Exactgame374323f8c3ef519694497371716e611d3396ee43, CI38963553875 SUCCESS:
424Domain+239API+155browser/1normal skip, frontend lint/build/DockerPASS.
PR49 mergedbbb5f2931af0f3e220fff23ea941d5f1e17f42dd, tested tree identical.
Botmetadata380d8ea3100a67834f28b8bac11d32377d6212f0 /CI38963592945 SUCCESS887tests,
serverSKIPPED. PR47 canonical mergee0a4cfa02ca8f8c34cc944ba11fe3d114016424a,
tree identical; only feedback version metadata changed, civic byte-identical.
20rollout guard testsPASS; local26focusedbrowser+TypeScript/lint/buildPASS.

## What changed

Slow fighting avatars keep fractional world coordinates instead of advancing in
whole world pixels. Avatars, enemies and health bars share bounded pacing for
late movement snapshots; health/order revisions preserve the current animation.
No extrapolation or changes to authoritative speed/damage/targets/collision.
Partial wall damage skips static-world/civilian processing until breach, gate or
layout changes. Additional display trail250–1000ms is the deliberate tradeoff.

Two actual owner diagnostic files read privately: packet gaps1502/1609.5ms;
second capture includes early framep95117ms, later~33.6ms. Full owner server
report peak simulation23.7ms, lock512.8ms. Read-only real-battle audit38960868199.
Backlog includes fixed-step subsecond clock offset; not evidence of continuous
2second network latency or CPU saturation. Raw diagnostic files are not published.

Actual VillageWorld regression: before9distinct actor positions/48renderframes;
after48/48 on both browser device profiles, delayed window30/30, healthbar drift0.
Headless regression proof is not physical-phone FPS evidence. Next natural battle
capture is still required; no forced production raid or synthetic player action.
Client timing export remains available in Settings; new export identifies0.7.5.

## Preservation and verification

119profiles before→119after,0progresslosses;121accounts/loginbindings retained.
25migrations,24publicfrontendchunks,feedback live version lookupPASS.
14protected botmodules/configs/quarantine/Propose/budget preserved. PostgreSQL
backup restore catalog verified; SQLite/code/config/profile snapshots saved
privately. No forced raids, synthetic messages, payments, model probes or resets.
No natural postdeploy combat timing sample at finalcheck; do not claim live FPS.
See RELEASE_0_7_5_RESULT.json. Do not reinstall or replay older rollout requests.
