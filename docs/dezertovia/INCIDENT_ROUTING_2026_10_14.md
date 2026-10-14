# Incident14.10.2026: public route served the legacy game

Owner reported the game unavailable and explicitly requested repair. Hospital
planning was postponed; no new game release or database reset was requested.

## Diagnosis

The public page returned200 but loaded legacy game.js/features.js/realm.js;
/dezertovia/auth/providers returned404 and lacked X-Desertovia-Release.
Private .NET health returned200; bot and housing services were active.
Bot deploy38588468426 at15:20 UTC used source without town_proxy.py and its two
routing hooks. It overwrote the server-only routing integration. The game service
and database did not need rebuilding; the public address had silently reverted.

## Immediate recovery

Operator38601966038 restored only the three previously reviewed routing files,
after checking source hashes. Check38602064917 returned local/public200,
Telegram/browser login enabled, development login disabled, bot/housing active.
Public release header again matched ad88be344908a7fb42bc434ce89aad94c92d1665 (0.5.1).
Protected API/SignalR routes returned401 for anonymous probes rather than404.
No new artwork, hospital mechanic, game release, candle changes or data reset.

## Prevent recurrence

Routing is committed in the canonical bot source (TG-bots PR31), preserving
existing chat/meme changes. The enabled route is checked before rsync, and
post-restart health compares the nonempty game release header through both
upstream and bot gateway. Failure uses the existing rollback.

Eight focused proxy/guard tests and shell syntax passed. CI38602556794 passed
770 tests, browser-client build and Node checks. Source:
c240b2768aa4dcb7dddac20405b59a6b387ff075. Guard deployment38603201121 succeeded: both preflight and post-restart checks
passed, with no rollback. Final operator check38603465467 succeeded. Public HTML
and auth/providers both expose release ad88be344908a7fb42bc434ce89aad94c92d1665;
the Vite frontend is served, not the legacy scripts. Telegram browser bridge is
enabled and development login is disabled.

Older feature branches lack the new deployment guard: rebase them before deploy.
Do not remove the upstream setting to bypass a failure. Bot source is mirrored to
its existing operator branch; housing main is untouched.
