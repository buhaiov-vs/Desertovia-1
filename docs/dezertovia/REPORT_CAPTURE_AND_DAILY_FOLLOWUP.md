# HOLD: report follow-up SOURCE ONLY — owner forbids deployment now

Owner18Oct2026: «Сейчас эти правки на сервер не грузи». This supersedes all
previous installation permission for these follow-up changes. DO NOT deploy,
change live configs/code, issue a rollout request or send synthetic reports/DMs.
Live0.7.15/52f2f6c and bot7963416a remain installed. No version bump for preparation.

User reportfe3ea7d4 inspected read-only: statusSent, one attempt, iOS0.7.15,
440x766 viewport,77,859-byteJPEG; mainmap entirely absent, HUD/minimap present.
Image inspected locally only; no real screenshot/account payload committed.
Reproduced in WebKit: visible map becomes absent in JPEG. html2canvas reads
img.naturalWidth/naturalHeight before the cloned data-URI image loads (zero size).
Await image.decode() in async onclone before parsing. Existing Pixi capture stays
unchanged; no extra game renders or preserveDrawingBuffer. Populated-map comparisons
PASS by day/night on desktopChrome and WebKit; no physical iPhone hardware check.

Owner also requests no individual report DMs: durablefeedback+identity/context
memory only, existing daily digest. No new timer/timezone/schedule. Preserve
historical receipts; legacy bridge Sent ack remains compatible with0.7.15 and
now means accepted into memory. New receipts Recorded, no Telegram call/model
call on intake. Screenshots stay in game PG storage with existing bounds/retention.
The game receipt explains the shared daily report. Old individual reports must
not be resent; new/original text enters daily summary without identity metadata
being forwarded to its model, full identity stays in owner-only report.
Preparation/tests/GitHub source only. Install only after a NEW owner instruction.
Historical release records follow.


## Verified source fix, NOT INSTALLED

Root cause: html2canvas ImageElementContainer snapshots naturalWidth/naturalHeight
synchronously. Replaced canvas data-URI images are not decoded yet on WebKit,
so the parser keeps a zero-sized map even when the live canvas shows buildings.
Await image.decode() in the clone before parsing. Original Pixi extraction and
live renderer/ticks stay unchanged. No new version number or server change.

Final populated village comparison:4PASS (desktop/WebKit, day/night) against
independent browser screenshots including Hut,StoneYard,ground,fence. Earlier
pending-image path reproduced blank WebKit captures. Additional menu/Cancel/X/
Escape checks2PASS; TypeScript/lintPASS. Native iPhone hardware not tested.
No actual player screenshots/private payload committed. Source only, ownerHOLD.
Bot counterpartPR95:11intake/dedup/daily tests+24existing feedback testsPASS.
No per-report Telegram calls; existing daily digest/schedule preserved. Pending
fullCI and installed state must remain distinct. Wait for NEWownerdeploymentinstruction.
