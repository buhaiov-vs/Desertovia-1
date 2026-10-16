# Owner correction package · D-081 · candidate 0.7.3

Status: implementation branch, validation in progress, NOT INSTALLED.
Live remains 0.7.2 / 04c395052a106562b8e00b6c73a8a5b119b1aea5.
The owner authorized code and tests. No rollout request or production event is created.

## Implemented behavior

- Own HP/stamina use the existing resource-card background, border and radius.
  Weather, morale and vitals share one status row and height; no separate full-width row.
  Owner rejected the first isolated stand appearance; it is superseded by shared styling.
  Permanent build/vote buttons and the duplicate menu footer are removed.
- Enemy selection sends one authoritative attack order, without mounting an enemy card.
  Combat-only retreat physically walks to reachable ground, remains vulnerable and
  stays withdrawn until explicit re-entry. Re-entry and queued waves preserve HP.
  Healing consumes one eligible remedy for 20% max HP, capped, with 60s cooldown
  and persisted request receipts. Disabled dark buttons expose the actual reason.
- Combatants interpolate consecutive battle revisions without civilian path catch-up.
  Large discontinuities snap; server positions and geometry remain authoritative.
- Voluntary demolition calculates actual suitable replacement beds, reserves distinct
  slots and moves residents. Another owner's private beds and ruins do not count.
  The same bounded reasons/counts reach the game and private Telegram cards;
  working applications can refresh to completed without repeating the operation.
- Real attacks target construction sites as well as finished structures/storage.
  Collapse retains site contributions, stored goods, level, research and player stats,
  releases construction claims and makes ruins nonfunctional until paid repair.
  Existing repair work costs 2 wood + 1 stone per cycle and repairs up to 5 damage.
  The 180 damage event cap is retained. Patients keep their recovery deadlines in
  a field infirmary if their hospital collapses. The royal bridge closes on collapse.
- New-world creation explicitly places a level-one castle at 14,9. Old-world loading
  and migration do not create/move castles or houses. Losing the castle persists a
  settlement defeat, ends this defense and cancels future waves without fake kills.
  Repairing the castle clears defeat; basic shelter, healing and recovery remain.
- The neutral monastery has a real arrival/bridge/knowledge checked exchange:
  10 wood + 6 food -> 1 remedy, once per account per village per 6h, with durable
  receipt and ledger. Residents use personal inventory, the Tsar uses public stock.
  Decorative cells do not provide beds and are not village siege buildings.
- The configured primary world opens directly at the app root. Old village/dungeon
  routes remain available. Multi-world configuration still uses world selection.

## Preservation and migration

20261016161632_OwnerCombatSiege adds nullable defeat/bridge ruin metadata,
BridgeDamage default 0, and only the required numeric Damage default in old site
JSON. It does not reset profiles, identity, skills, health, materials or injuries.
Existing worlds keep their actual castle location. A proposed relocation for an
existing production world still requires a concrete read-only map review; this
package deliberately does not perform that move.

The bot runtime change is confined to civic_notifications.py. No donation, voice,
moderation, AI-budget, director or payment implementation is changed.
The earlier unrelated audio test failure is recorded in local validation history;
its isolated rerun passed, and the full repeat must succeed before readiness.

## Validation record

- Domain: 417 PASS, including low-damage castle defeat and no fake enemy kills.
- Final local API run: 235 PASS, including old JSON migration and cancellation of future waves after castle defeat.
- EF pending-model check: PASS, no model differences.
- Isolated revised HUD: 5 PASS at 360/390/430/1440px, including disabled healing.
- Earlier browser build: 44 PASS / 10 failed in an incomplete new harness fixture.
  The fixture is corrected; those failures are not described as a successful run.
- Real-map shared-style check PASS on phone; direct enemy-selection attack check PASS.
  Initial attack probe ran before local backend readiness (502); fresh isolated retry PASS.
  Desktop screenshot and final production build/browser CI are pending.
- Bot civic focused: 8 PASS; full repeat: 887 PASS. Exact PR CI pending.

No physical-phone FPS measurement has been made. No universal lag-free claim.
Director economic/tax executors and owner digests remain their own open backlog.
All new checks use local data/mocks, no real messages, paid probes or production raids.
