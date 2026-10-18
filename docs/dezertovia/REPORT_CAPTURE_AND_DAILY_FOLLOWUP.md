# HOLD: report follow-up SOURCE ONLY — owner forbids deployment now

Owner18Oct2026: «Сейчас эти правки на сервер не грузи». This supersedes all
previous installation permission for these follow-up changes. DO NOT deploy,
change live configs/code, issue a rollout request or send synthetic reports/DMs.
Live0.7.15/52f2f6c and bot7963416a remain installed. No version bump for preparation.

User reportfe3ea7d4 inspected read-only: statusSent, one attempt, iOS0.7.15,
440x766 viewport,77,859-byteJPEG; mainmap entirely absent, HUD/minimap present.
Image inspected locally only; no real screenshot/account payload committed.
Fix copies an explicitly rendered visible frame synchronously to a temporary2D
canvas, avoiding offscreen stage extraction. Validate populated terrain/buildings/
fence against independent browser screenshots in day/night, desktop/WebKit.
Native iPhone GPU cause not yet proven; do not claim physical-device verification.

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


## Validation status

TypeScript and lintPASS. Real menu/Cancel/X/Escape checksPASS on desktopChrome
and WebKit with the new visible-frame capture. Populated-map JPEG comparison
against independent browser screenshots is being finalised for day/night on
both engines. Initial comparison script corrected to materialise its resize
before extracting; that fixture error did not affect game runtime. Actual JPEGs
show Hut/StoneYard/terrain/fence; native iPhone GPU still needs later confirmation.
No source version bump, deployment or live change. Bot counterpart PR95, memory
and existing daily digest only.
