# 0.7.7 INSTALLED and independently verified — 17 October 2026

Owner D-087 authorized installation no earlier05:30UTC/07:30Europe/Oslo.
Fresh preflight39006496030 SUCCESS; install39006543221 SUCCESS;
independent check39006763680 SUCCESS at2026-10-17T05:34:46.7429731Z.
LIVE game44b4e426cdfdd9bb72621a9febeb624130a8d6e2, bot payload source
d462e634df7ac49efd45b1ab3bc09e49fd11796b. Exact CI38979407726 and38980131645
rechecked authenticated: completed/success/attempt1; tested/merged trees match.
119→119profiles,0lost profiles/progress;121accounts and121login bindings retained.
26migrations includingPlayerFeedback077; all4building-number checks0;24public
frontend chunks and feedback live version0.7.7 verified. PostgreSQL dump restore
catalog verified; SQLite/config/code/profile backups retained privately.
14protected bot modules/configurations/quarantine/AI budget and directorPropose
preserved. Only civic_notifications.py/feedback_context.py replaced in bot.
No synthetic messages, model probes, forced raids, payments or progress resets.
No natural battle timing sample at finalcheck: physical-phone FPS still unverified.
Do not reinstall or replay older requests. Earlier READY/NOTINSTALLED entries
below describe preparation, not the current runtime.

Actual receipts: [result](RELEASE_0_7_7_RESULT.json).
Installation: https://github.com/desertod666/bolig-agent/actions/runs/39006543221
Independent check: https://github.com/desertod666/bolig-agent/actions/runs/39006763680

## История подготовки и состав изменений

# 0.7.7 — READY, установка запланирована, ещё не установлено

Разрешение владельца D-087: запуск **17 октября 2026 в07:30 Europe/Oslo
(05:30UTC), не раньше**. Одноразовая задача создана. Сейчас сервер0.7.6/d0f88e2;
публикация исходников и готовый candidate не являются установкой.

## Исходники и проверки

Game exact44b4e426cdfdd9bb72621a9febeb624130a8d6e2, CI38979407726 SUCCESS:
439Domain,251API,167browser PASS/1штатныйskip, lint/build/DockerPASS.
PublicPR51 merged6540b982f753c5ca8c99eb9e986428b0b55fd546; дерево совпадает с tested.
Bot exactd462e634df7ac49efd45b1ab3bc09e49fd11796b, CI38980131645 SUCCESS:
890tests, serverjobSKIPPED. CanonicalPR49 merged422cb81573cc51e79053aee285be269ef7c069db,
дерево совпадает с tested.24operator guards PASS; readonly SQL нумерации на
локальном PostgreSQL: пропуски, дубли, недостаточный счётчик — все0.
Ранний botCI1168/38979537591 упал на зависевшем от ночного достижения тесте
лимита свечей; исправлена изоляция теста, правила валюты не менялись.

## Изменения

Постоянные номера зданий, питание из рук/личного запаса и кнопка «Поесть»,
ремонт выбранного здания по10%, разгрузка в выбранный общественный склад,
заселение в собственный дом без царской заявки, владелец/дата готового дома,
назначение добычи, обе характеристики работы, бесплатная передача личных
материалов в общий запас. Рабочие маршруты/прогулки обходят воду; небольшое
окно результата работы больше не включает лимит карты8FPS. Согласование
общественного жилья, права собственности и медицинские ограничения сохранены.
Новые ещё не одобренные здания пока не имеют номера: детали участка сохранены
для проверки размещения. Координаты остаются внутренними ключами.

Полный план/дополнительные репорты: [PLAYER_FEEDBACK_0_7_7.md](PLAYER_FEEDBACK_0_7_7.md).
Карта, рецепты/исследования и профессии — отдельные пакеты. Физический FPS
телефона требует следующего обычного наблюдения, без тестового рейда на сервере.

## Утренний выпуск

Приватный bolig-agent, только ops/almighty-release:
`deploy/PLAYER_FEEDBACK_0_7_7_RELEASE.md`, `post077-candidate.json`,
`post077_release.py`, workflow`post077-rollout.yml`. Candidate.ready=true.
Только два bot runtime файла: civic_notifications.py и feedback_context.py.
До07:30 никакого post077-request.json. В срок заново проверить фактические CI,
источник, baseline, файлы и службы; report→deploy→отдельныйcheck. Обязательны
актуальные PostgreSQL/SQLite/config/code/profile backups и preservation receipt.
Любое чужое изменение или сбой обязательной проверки останавливает установку.
Не менять private main, не использовать старые requests/pins, не запускать
синтетические сообщения/платежи/рейды/модельные пробы и не сбрасывать прогресс.

Фактические install/check run IDs пока отсутствуют. После установки обновить
этот документ, JSON receipt и AGENTS реальными результатами. Не выдавать
scheduled за installed.
