# Cooperative update0.5.2 — source and validation record

15 Oct2026. Repositories only. NO deployment, live SQL, real wallet operations,
Telegram messages or resets. Candidate in feat/cooperative-update in both repos.
Author .NET/EF/PostgreSQL + React/Pixi/SignalR architecture preserved.

Game source baseline3356a36; prepared hospital45cfccd + selected outline/departure/
undead changes. Main implementation checkpoint5bc913b; final branch adds review
fixes, patient layout and tests. Bot baseline8239cf9 retains proxy/tone guard;
merge154262b + finalc168c01. Bot PR34:
https://github.com/desertod666/TG-bots/pull/34 (CI test successful, server skipped).
Game review: https://github.com/desertod666/Desertovia/compare/main...feat/cooperative-update.

## Implemented contract

| Blocks | Main implementation |
| --- | --- |
|1,18| Prepared hospital +52 baked identical-outline PNGs + shared2x undead geometry; no new art project or runtime outline |
|2–4| BankAccess guard for walking/direct work/exploration; mobile distinguishes fixed Tsar crossing from RopeBridge; RulesVersion2 only for new six-stage bridge |
|5,6| Building skill gates1 for first bridge,3 later; slow work preserved; useful XP/renown, exact weighted stage pools, supervision snapshots count real progress; unique crew by account |
|7–9| BuildingOperations/CivicOrders, storage upgrades, wear/repair, safe staged demolition with owner final approval; only known investment refund, no XP for demolition |
|10,11| Longhouse12/20/28 beds, stable HomePlotId; signed read-only reciprocal bot marriages by Telegram ID, consensual joint housing and owner approval |
|12,13| Hospital rules/worker/UI/assets; immutable external episodes, paused work, durable payment receipts, bounded once-only free-ward outcomes |
|14,15| Work-only tired×3, XP25%,3 finished exhausted cycles→12game-hour faint; ordinary regen/walking; qualified vigor brew and4-hour cooldown |
|16,17| Silent .NET chaplain without fresh human conversation; verified facts and repeat/rate memory; Cyrillic/system fonts, compact selected-object actions, independent UI darkness |
|D-042| Native wounded transport/helper action,5-minute unclaimed NPC evacuation, camp ward fallback, lying poses and separated patient labels/cards |

Map wards show up to4 staggered patient places, always retaining own patient;
all patients have individual full-name/cause/countdown cards in hospital. No long
row of overlapping names. Large injury icon remains for field rescue, not ward beds.
Battle Remedy/wait is separate from existing paid duel treatments. Transport never
heals, changes the original deadline or removes inventory. Native rescue works
without enabling the external hospital integration.

## Data preservation

Additive migrations: CooperativeVillageUpdate (new columns + JSON defaults and
backfill of completed old bridges), WoundedTransport, SupervisionWorkSnapshot.
Legacy projects retain RulesVersion0, old costs/progress. Storage goods above new
caps remain withdrawable; no truncation. Legacy unknown investment yields no
invented demolition refund. No user progress reset. Removed destructive buttons
are UI protection: existing author leave/delete API still exists.

Migration regression uses a disposable PostgreSQL container: downgrade to the
pre-package AddExternalRecovery schema, remove new JSON defaults, then upgrade;
assert completed bridge open, Building3/47XP and Renown123 preserved,70 wood above
new cap retained/withdrawable. Live migrations were NOT run.

## Local verification after source phase

- Full Domain suite321/321 passed before the final review fixes; final targeted
  project/cooperation suite28/28 includes4 new cases: weighted exact pool,
  account-unique crew and useful/idle supervision.
- Full API suite174/176 initially passed. Two failures were an old far-bank walk
  expectation and PostgreSQL timestamp precision in a new transport test. Fixed
  both, with6/6 focused rerun; after final migration/review9/9 targeted cases passed
  (hospital API, rescue, civic permissions, legacy migration and walking).
- Bot28 isolated hospital/relationship/proxy/tone tests passed. Bot PR34 full test
  job successful38706625050, deployment job skipped.
- Frontend production build and lint passed; existing warning-level React notices
  remain. A false hook name in the native remedy click handler was corrected.
- PC/phone UI suite21 cases initially passed;1 CPU/WebGL-heavy phone case timed
  out in teardown. Isolated rerun passed, alongside hospital receipt/320px cases
  and real Pixi bed/discharge test (4/4). Not a live-server check.
- Final phone hospital checks3/3 passed, including pending receipt reuse and
  four patients on a320px real Pixi canvas. A final visual review increased bed
  spacing, removed ward injury bubbles and clamped text labels; the resulting
  targeted phone test passed1/1 after those fixes (16.8s). Image with synthetic
  patients: [320px ward layout](validation/cooperative-patients-phone.png).
  Authoritative final CI is recorded in the PR checks.

CI remains required for the final branch (backend, frontend, browser and Docker).
Do not substitute the earlier SHA/checkpoint results for the final PR status.
All fake payments and identities are confined to local test harnesses.

## Remaining operational boundaries

Game__HospitalEnabled defaults false. Coordinated bot+game installation and
configuration are future release work, not part of this source-only task. No
end-to-end real Telegram charge was performed. Startup balance is specified,
not calibrated from live telemetry or a guaranteed multi-day construction timer.
Commander/castle/new catastrophes and real currency exchange remain future work.
