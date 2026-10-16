#0.7.5 movement fix: implementation and evidence

INSTALLED0.7.5; see RELEASE_0_7_5.md /RELEASE_0_7_5_RESULT.json. Owner clarified camera/buttons stay
smooth while fighting avatars stutter. Requested diagnostic files were read
locally; raw reports are not committed or uploaded publicly.

## Observed evidence

Real battle after0.7.4, read-only private audit38960868199:8enemies,7fighters,
server simulation generally~1ms, full exported peak23.7ms; peak lock wait512.8ms.
Client capture1: packet gap1502ms, updatep954ms, worldp951ms, framep9547ms.
Capture2: packet gaps up to1609.5ms; early framep95117ms and worldp9541.2ms,
later framep95~33.6ms. Both actual touch devices; not synthetic FPS measurements.
Reported backlog~1.8sec includes the fixed-step clock's subsecond offset; do not
interpret it as continuous two-second network latency or CPU saturation.

## Reproduction and changes

Actual VillageWorld render test with deterministic moving fighter and delayed
snapshots: before change9distinct drawn positions over48frames. Explicit world
pixel rounding made slow movement visibly quantized despite smooth canvas/UI.
After change48/48frames change position on both browser devices, delayed window30/30,
healthbar drift0. Keep fighter and carried icon coordinates fractional; final renderer pixel
alignment preserves artwork. Idle civilian rendering remains as before.

A1000ms segment stopped before1.2–1.6sec snapshot gaps. Both fighter bodies and
enemy/bar rendering now share bounded pacing: recent movement arrival cadence
with a250ms cushion, gradual decay and maximum2000ms. Health/order revisions do
not reset motion. Idle gaps/teleports reset cadence. Never extrapolate beyond a
received point or modify server speed, damage, target, collision or authority.
Displayed motion can trail authoritative position by an additional250–1000ms;
that is a deliberate tradeoff for fewer stop/start transitions under jitter.

Wall health changes below ruin threshold do not change visible geometry. Skip
static-world/citizen work for those packets; destruction/gates/topology still
invalidate normally. This matches the existing project renderer geometry key.

Tests: deterministic real-map motion incl late packets/healthbar alignment;
99wall damage snapshots with no civilian refresh, breach/gate refresh required;
stale/health-only/overlap/teleport snapshots; enemy deaths and selection use actual
drawn coordinates. TypeScript/lint/focused browser and full exactCI are required.
No assertion of complete live/device resolution until next natural battle capture.

Local TypeScript, lint (existing warnings only) and production frontend build PASS.
Full exact CI38963553875 and fresh preflight38965661962/install38965752003/independentcheck38966145677 PASS.
Focused26browser tests PASS, including actual enemy click targets on displayed sprites.
