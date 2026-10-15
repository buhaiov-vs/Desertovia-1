# D-056 — единый Всевышний0.6: реализация

В РАБОТЕ,15.10.2026. Владелец разрешил включить механизм и присылать ему через
батюшку предложения с согласием/отказом. Это разрешение личных owner-уведомлений,
не публичного флуда/денег/реальных тестовых платежей. Старые PLAN/Off перекрыты.

## Выбранные правила

- Enabled=true при выпуске для Hosting:PrimaryVillage, режим Propose.
- Дешёвый worker60с. Стратегический запрос15мин, значимые изменения от5мин;
  максимум4/час и96/реальные сутки, учитываются неуспешные попытки.
- Общее предложение владельцу не чаще30мин; expiry30мин; отклонённый/уже
  применённый тип не повторять2ч. None допустим, пустой чат не повод для флуда.
- Передышка2ч, минимум1ч между модельными угрозами по любому предыдущему рейду;
  поражение2ч исключает угрозу, пациенты/онлайн/охрана проверяются свежими.
- Одна личность/память/LLM chain; стратегический выбор и голос батюшки через
  один ограниченный bot endpoint. Сохраняются прежние глобальные бюджеты бота.
- Только подтверждённые владельцем эффекты. Экономический совет принят — это
  запись, не автоматическое изменение денег/налога/курса.

## Готовая основа в коде (ещё не выпуск)

Game branch feat/almighty-patchnotes, PR33; предыдущие notes/D-053 входят.
Domain Director/Almighty.cs: состояние, предложения, наблюдения, policy.
GameService.Almighty: world lock/lease, snapshot, memory, budgets, owner votes,
recheck условий и идемпотентное применение. AlmightyService/Worker: transport,
owner-only notice outbox, signed callback. EF migration20261015152237.
Ночной worker уважает принятую передышку, уже объявленные волны продолжаются.
SramoslavService при Enabled использует тот же контур для речи.

Domain Combat/RaidTactics: bounded BFS по западной стороне реки, построенным
footprints и uncleared obstacles,3 подхода; raid хранит маршрут/сектор. Скелетная
направленная волна — выбор того же ИИ. Guard deployment30реальных секунд,
чужой сектор не защищает подход; Auto резерв75% личного вклада против такого
рейда. Старые ненаправленные волны сохраняют прежний расчёт. Клиент рисует
последние7 точек серверного маршрута после таймера, отдельный выбор охраны.
Это первый серверный боевой срез, не полный симулятор нескольких отрядов/стен.
Существующие стены/башни, которых ещё нет как пространственных объектов,
не объявлять уже способными участвовать в этом расчёте.

Опции: none, economy_report, rest, work_notice, zombies_small,
skeletons_small/направления, добровольный сбор в первый существующий Dungeon.
Караваны/новые денежные сценарии требуют собственных правил; не создавать
неограниченную эмиссию. Сводка читает остатки и прошлое наблюдение; точную прибыль
без полного ledger не выдумывает. Свечи/износ/скорость работы не изменяются.

Bot worktree /workspace/TG-bots-almighty, branch feat/almighty-director,
base current canonical01c0547, НЕ старые feature refs. Изменения:
almighty.py, town_proxy.py, app.py (+docs/tests). Signed director endpoint,
persisted receipts/limits, owner-only private inline buttons, signed vote назад
в игру. Неизвестная доставка не повторяется автоматически. Callback retry
получает тот же результат; бот не становится владельцем игрового состояния.

## Проверки и оставшаяся работа

На промежуточной основе:16 bot tests PASS (4new+12proxy),3 Domain PASS,
3 real PostgreSQL API PASS: права, lease/concurrency/idempotent approve,
поздний ответ после Off. Компиляция .NET и web прошла до последних малых правок.
Сейчас итоговые source/build/UI/bot tests ещё продолжить; ничего не выложено.

Нужно закончить: UI стенд/финальные проверки, positive Telegram callback и
проверку свежих условий нападения; release docs/patchnotes/version0.6;
CI обоих репо; приватный reviewed deploy поверх текущегоa980d8d0.5.3:
fresh snapshots/backups → только3 bot runtime файла → migration/game → Enabled.
Убедиться в сохранении100+профилей/прогресса, Telegram/hospital/proxy, модельном
review и допустимом owner notice. Без лишнего тестового сообщения/платежа.

Deploy worktree /workspace/bolig-cooperative-release, canonical private
ops/cooperative-release. Старый regression_release.py жёстко0.5.3: не вызывать
его deploy для новой версии без нового pin/guard. Нужен отдельный0.6 оператор
с exact CI и source pins, fresh pre-install snapshot и проверкой18 migrations.

Последнее перед публикацией: локальные333Domain/185API PASS,6 targeted API,
6 browser PASS; bot793full/18targeted PASS. Игровые каталоги теперь динамические,
EnemyDefinition.Coordinated/RaidCountMultiplier, автоматические варианты и
сводка вещей/оружия/рецептов. PublicDirectorMemory даёт речи только уже
применённые факты, не секретные pending планы. Полные snapshot не уходят в
телефонный API; Title/Effect карточки вычисляются из сохранённого каталога.
Подготовлен private operator deploy/almighty_release.py, новая ветка
ops/almighty-release. Runtime исходники ещё не выложены. Далее exact CI и выпуск.
