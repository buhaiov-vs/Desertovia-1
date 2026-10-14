# Night buildings, village chaplain chat and phone fixes

Owner authorization 2026-10-14: connect the 26 prepared building pairs, make the
transition continuous, and restore the first Desertovia's short village chat
conversation. Later screenshots added the narrow bottom-panel text and stock
quantity field bugs. Implementation branch `feat/night-buildings-chaplain-chat`.
Status: implemented and locally checked; deployment pending.

## Rendering

`web/src/game/BuildingLighting.ts` uses the current Pixi renderer, its world
positions, depth sorting and camera. Original atlas scale2 is accounted for;
night atlas scale1. The night atlas is split into two textures no larger than1536px,
compatible with older2048px GPU limits. Touch rendering is capped30FPS; desktop60FPS,
with8FPS behind open modals to keep touch controls responsive. Server action and
cycle timers are unchanged. Registration aligns visible roof/wall bounds and their ground
anchor for each pair, excluding new side lanterns for wood/stone piles. One mesh
interpolates premultiplied RGBA; overlapping opaque walls remain opaque at dusk.
The completed project ID `granary` maps to `great-granary` art; unknown future
project types keep the original tiled renderer instead of failing. Owner subsequently requested stronger building light like the campfire: warm-only
window/lantern emission and layered warm ground pools are rendered additively
above darkness and below fog. Light opacity follows the same continuous night
amount, with a slight slow flicker. Night art includes warm glow; the shader compensates the existing blue overlay
so lit windows are not darkened twice. Textures load together before creating the
world, and views rebuilt by server updates inherit the current night amount.
`darknessAt` already gives a continuous twilight curve across server day changes.

Completed structures, storages, both completed project buildings and monastery
use the new art. Existing site stages, crop overlays, storage contents, damage,
click targets, fog, bridge renderers and animation remain in the author's renderer.
This is the building-art integration, not a replacement of all scenery or UI.

## Shared chat

`ChaplainChatWorker` polls every5s, enabled for our configured primary world (or
explicit `Game:ChaplainChat:Enabled`). Explicit calls: батюшка, Срамослав, отче,
@sramoslav_bot. The existing private bridge supplies AI responses; ambient speech
uses short local phrases. One global gap90s, maximum12 published lines/hour,
ambient gap8min, presence plus work/chat activity required for ambient speech.
Player calls and replies are shared; only members can receive history/SignalR.
Old 22-second client-local ambient bubbles are replaced by shared chat speech.

`ChaplainMessages` is a new additive table with village/text/time only. No bot
account, citizen, wallet or governor/Tsar role. Old `ChatMessages` stays unchanged;
merged history retains the latest50 lines. Durable NPC timestamps retain flood
limits across restarts. Existing bridge independently limits AI calls to12/hour.
Replies are capped120 characters; no Telegram send, TTS or game commands.

## Phone fixes

Phone `.hud-status` places `.next-action` on a full-width row; buttons wrap in
separate equal grid columns. Tests include 320px and130% text.
`StorageCard.TakeRow` stores draft text: clearing stays blank; entering2 stays2
through stock refresh and clicking Take. Validate integral amount1…current limit
before enabling Take; never silently substitute a different amount.

## Verification and rollback

Local backend: 306 domain +168 API =474 passing. API test verifies NPC identity,
membership, reconnect history, restart gap and persistent hourly cap. New browser
checks cover all26 pair geometry/opacity, editing/refresh, phone text and NPC
ownership. Existing lint warnings remain. Full suite/CI and visual game screenshots
are recorded at release completion.

Additive migration only: existing accounts, player progress and chat are preserved.
Rolling back the API does not require deleting NPC rows or reverting this table;
the previous API ignores it. Keep the normal database backup before deployment.
No game reset and no economy, candles or authentication changes in this release.
