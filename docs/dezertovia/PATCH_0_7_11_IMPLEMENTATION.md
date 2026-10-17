# 0.7.11 — рабочий план и продолжение после сжатия контекста

17.10.2026, D-097. Владелец разрешил приступить к реализации всего плана.
Последнее уточнение: заборы сохраняют стиль игры, обводку, цвета и ночной вид.
**IN PROGRESS, не READY и не установлено.** Отдельной даты установки 0.7.11 нет.

## Обязательные источники и порядок приоритетов

1. Актуальные AGENTS.md/CLAUDE.md и этот документ: фактический прогресс.
2. NEXT_PATCH_PLAN.md: полный объём D-093–096; ничего не терять при сжатии.
3. RESCUE_AND_RAID_CONTROLS_0_7_11.md: точные правила переноски/пресетов,
   отменяет прежнюю автоматическую госпитализацию через 5 минут.
4. RAID_DIFFICULTY_NEXT_PATCH.md: бюджет, исходы, адаптация и стартовые числа.
5. FEEDBACK_REVIEW_2026_10_17.md: 117 отзывов и пять сообщений владельца,
   что уже исправлено/требует воспроизведения/отложено.

Версии: 0.7.11, затем 0.7.12…0.7.99; не превращать небольшую правку в 0.8/1.0.
Исторические receipts 0.7.9 не переписывать как успешные проверки 0.7.11.

## Рабочая среда и ветки

- Игра: /workspace/game079-record, feat/patch-0-7-11, public Desertovia.
  Основа реализации — main 0df8eb935d877db809feec55c563ec24d3a73391,
  код соответствует установленному 7c738bc плюс последующая документация.
- Бот: /workspace/bot079-final, feat/patch-0-7-11, canonical TG-bots
  claude/telegram-bot-chat-variety-wci5ph, основа e0dc839.
- Private ops: /workspace/release077, push только ops/almighty-release.
  Существующий post079-request action=check не менять и не переигрывать.
- Не редактировать старые feature worktrees /workspace/monster-behavior и
  /workspace/bot079-record вместо текущих веток. Других агентов не запускать.
- .NET: /workspace/dotnet/dotnet, SDK 10.0.300; использовать DOTNET_ROOT.
  Не запускать Vite build и .NET static assets build одновременно.
- Python-зависимости установлены в /workspace/bot079-final/.venv; полный
  актуальный набор 894 tests PASS, не подменять старым 891 tests.
- Сохранять proxy/CA и auth; ключи/сырые отзывы/БД не выводить и не коммитить.

## Пакеты реализации и контрольные точки

| Код | Что выполнить | Статус на начало D-097 |
| --- | --- | --- |
| FB-01 | Настоящий capture просьб владельца до Helper.answer, номер только после commit, источник/проект/версия, idempotent Telegram message, обычный разговор/настройки не поглощаются | BRANCH: 894 bot PASS, 40 focused PASS |
| FB-02 | Неполный «баг:» требует описания; не обещать запись без содержания; не выдумывать отсутствующий screenshot | BRANCH: empty-prefix clarification tested; media remains existing |
| MED-01 | Убрать RescueSweep auto-Admit через 5 минут; конечное уличное самолечение | BRANCH 8d94003 |
| RESCUE-01 | Реальный путь от текущей позиции, подбор и переноска в доступную больницу без 5-минутного CraftLength и без окна допуска после ранения | BRANCH 8d94003 |
| RESCUE-02 | При приёме однократно остаётся 25% оставшегося срока; ранее прошедшее время сохраняется, не удлинять уличный baseline | BRANCH 8d94003 |
| RESCUE-03 | +2 славы и обычный XP за реальную полезную доставку один раз на эпизод; не двойная зарплата, не реальные свечи/Dcoin | BRANCH 8d94003 |
| RESCUE-04 | Два спасателя/отмена/перезапуск/ранение помощника/выздоровление в пути; нет вечной брони, offline не отменяет серверную работу | BRANCH 8d94003 |
| RESCUE-05 | Царь как существующий actor; внешнее лечение только через его источник, не подменять paid-duel таймер | BRANCH 8d94003 |
| RESCUE-UI | Попросить помощь/маркер/короткое сообщение без спама, отнести/статус спасателя; улица видна, палата внутри, реальное время | BRANCH 8d94003 |
| RAID-EDIT | БОСС-ФАЙТ (1–2, до 3), Смешанный бой, Орда (до 48 одновременно); редактировать и сохранять собственные пресеты | BRANCH bc02048 |
| RAID-CONFIG | Виды/число/доли/HP/урон игрокам и имуществу/скорость/сторона/волны/предупреждение/таймер/отступление/кража/босс-фаза и удар | BRANCH bc02048 |
| RAID-PREVIEW | Безопасный preview без волн/списаний/модели, auto и owner-only manual override, явная оценка риска, единичный запуск | BRANCH bc02048 |
| RAID-PERSIST | Полная неизменная конфигурация/инициатор/история; старый JSON совместим, отмена всех волн, эксперименты не искажают адаптацию | BRANCH bc02048 |
| RAID-OUTCOME | Отделить ущерб от разгрома; 50 зданию/100 стенам не означают тяжёлое поражение и запрет на 2ч | BRANCH bc02048 |
| RAID-ADAPT | HP/броня/реальная оборона; после двух лёгких +10%, после тяжёлого −15–20%, ориентир 75–150%, потолок по развитию | BRANCH bc02048 |
| RAID-LOSS | Масштабируемый ущерб (начальный ориентир 180–360), забор/кражи/износ, конечное восстановление, живой носитель и однократные списания | BRANCH bc02048 |
| RAID-TACTICS | Роли/обходы/проломы/2 узких входа/цели; до 4мин обычный/6мин осада, различать отступление; без model calls на тике | BRANCH bc02048 |
| RAID-REPORT | Фактические участники/HP/лечение/ранения/урон/кражи/стрелы/износ и причина следующей сложности; Propose/одобрение сохраняются | BRANCH bc02048 |
| NUM-01 | Стабильные видимые номера по типу, единственный без номера; старый глобальный ID/история/ремонт/заявки не ломаются | TODO |
| FOOD-01/02 | Больше полезного расхода после замеров, один голод, ноша/личное/общественное по правилам; ручная еда и переключения без reload | TODO |
| WALL-01 | Дерево→брёвна→камень, параметры/работа/ремонт/ворота/навигация; не бесплатное исцеление старой стены, железо позже | TODO |
| WALL-ART | Общий масштаб/палитра/обводка/пиксели, день/ночь, прямые/углы/окончания/ворота/повреждения/разрушение | TODO |
| SPACE-01 | Обнаруживаемость расширения замком, улучшения вместимости жилья/складов, без перемещения построек/самозабора, без обещания полной карты ×2 | TODO |
| XP-01/KNOW-01/COOK-01 | После P0/P1: боевой XP силы/ловкости, исследования в открытия/рецепты, 2–3 блюда без бесконечного сложения | TODO |
| REG-01 | Свежая проверка старых еды/ремонта/склада/личного жилья, камыша/квеста/воды/рывков по воспроизведению | TODO |

Каждый пакет заканчивать кодом + подходящими регрессионными тестами + записью
реального результата ниже и в progress.json. Не помечать TODO как PASS по наличию
кода. Полные suites запускать в конце и после существенных регрессий, не после
каждого изменения документа. Незавершённые P2 обозначать явно, не забывать.

## Графика забора — обязательный контракт владельца

Сначала осмотреть существующие текстуры и ночные atlas/sheets. Не заменять стиль
другим набором целиком и не трогать уже утверждённую графику зданий.

- Сохранить пиксельную сетку, перспективу, высоту/якорь на клетке и стыковку
  соседних прямых, углов, концов, ворот и створок.
- Обводка той же толщины и тёмного оттенка, без размытия/ореола и сглаживания.
- Палитра: деревянный нынешний, бревенчатый в тех же коричневых тонах,
  камень согласован с существующим каменным двором/складами, не фотореализм.
- День и ночь каждого варианта. Ночью общий свет/цветовой сдвиг как у мира;
  каменная стена сама не светится. Плавный переход по существующему nightAmount.
- Варианты целый/повреждённый/разрушенный, проход ворот и тени; прозрачный фон.
- Проверить на настоящей карте в обоих режимах и при разных масштабах; PNG
  и atlas без белых фонов/пустых полей/прыгающих границ. Источник/экспорт/промпт
  и лицензии хранить рядом с art, не внедрять случайный нечитаемый sprite sheet.
- Для генерации/редактирования использовать image tool, перед редактированием
  локальных референсов осмотреть их. Упаковка/валидация допустимы скриптами;
  не выдавать красивый исходник за готовые проверенные игровые тайлы.

## Точки входа кода

Бот: sramoslav/app.py::on_private, feedback.py::capture/dm_feedback,
db.py::add_feedback, tests/test_feedback_context.py и test_bot.py.
Здесь причина подтверждена: owner Helper.answer вызывается до dm_feedback.

Спасение: Domain/Rules/RescueRules.cs, CycleEngine.StartRescue,
Api/Game/GameService.Rescue.cs, HospitalRules, Citizen, VillageWorld, карточки.
Сейчас Admit не меняет deadline; fallback через 5мин делает помощь бессмысленной.

Бой: Combat/RaidBalance.cs, RaidSimulation.cs, RaidBattle.cs, RaidRules.cs,
MonsterBrain/Crowd/Navigation; GameService.Battles/Almighty и DTO, RaidBanner,
GameMenu, GamePage, web API. Один владелец состояния .NET/EF/Postgres сохраняется.

Остальное: Buildings/BuildingNumbers/KingdomRules, StorageLimits/Rules,
FoodRules/CycleEngine, Fortification, worldMap, VillageWorld/sheets и art.

## Сохранность и выпуск

Не сбрасывать профили/навыки/вещи/ранения/работы/содержимое складов/валюту/
login bindings/старые номера. Новые JSON поля с legacy defaults, миграции additive.
Потери рюкзака и постоянная смерть отложены. Без дозоров и принуждения к костру.

Проверить Domain/API, preview/права/idempotency, внешнее лечение, сохранения,
телефон/ПК, build/lint и нагрузку 16/32/48/боссы. Публиковать feature branches и
reviewable PR, не заменять tested source текущим main. На сервере не проводить
синтетические бои/сообщения/платежи и не менять реальный Propose/AI budget.
Новая установка требует отдельного актуального пакета/preflight/backups/check;
не использовать старые post079 pins/request для 0.7.11.

## Фактический журнал работы

- D-097: прочитаны актуальные инструкции; созданы обе feat/patch-0-7-11.
  Реализация начата с фиксации спецификации/графики/контекста. Проверки кода
  ещё не выполнены. LIVE по последнему verified receipt остаётся 0.7.9.

- FB-01/02: app/feedback/helper/db, SQLite origin receipt атомарно с отзывом,
  forget удаляет receipt. Восемь новых тестов; 40 адресных PASS. Первый полный
  прогон 893/894 из-за старой фикстуры с одинаковым message_id для разных текстов;
  исправлена уникальность сообщений, повторный полный прогон 894 PASS (145s).
  Сообщения/база рабочего бота не менялись.
- MED/RESCUE: серверная первая версия в работе; 11 Domain rescue/hospital PASS.
  Новые поля ещё требуют миграции; UI/царь/просьба помощи и полные проверки
  не закончены. Не выдавать частичный пакет за готовую 0.7.11.

- Social rescue packet implemented in branch: no automatic admission; saved
  physical A* route with real bridge/gates, pickup/arrival, 75% remaining-time
  cut once, +2 renown for eligible enemy wounds, zero reward for admin wounds.
  Paid work resumes once; helper injury drops patient at current battle position.
  Tsar uses existing account/village fields, no persisted extra Citizen.
  Help request is a rate-limited in-game chat action, not Telegram messaging.
  Frontend shows actual rescue route/status and hides admitted patients indoors.
- Additive migration 20261017100421_SocialRescue0711: nullable rescue JSON/dates
  and false-default reward flags, no profile/data rewrite. 28 migrations now.
- Checks: full Domain first pass 466 PASS; latest focused rescue/hospital/live
  raid 31 PASS including 3 additional cases; API civic/hospital 13 PASS;
  browser rescue/patients 8 PASS across desktop/phone; frontend build and TS PASS.
  First browser attempt did not run cases because Chromium was missing; installed
  the matching Chromium and the repeated 8-case suite passed. Full bot 894 PASS.
- Browser test DB: local Docker patch0711-e2e-postgres, localhost:55471,
  database desertod_e2e, dev credentials already in playwright config. Never live.
  Test logs /tmp/patch0711-rescue-*.log, /tmp/patch0711-domain-rescue.log,
  /tmp/patch0711-bot-tests-final.log. Node modules symlink to monster-behavior/web.
- Old art/generated/project-palisade-01 README explicitly rejects those draft
  tiles (haze/seams/scale); DO NOT silently integrate them as the new wall art.
  Inspected current tiny-town, hut preview and night buildings-a atlas as refs.
- Next packet: RaidSettings.cs skeleton started but NOT yet wired/tested.
  It stays separate from the social-rescue commit. Finish preset planner/API/UI,
  immutable per-battle settings/individual species and full adaptive block before
  calling 0.7.11 ready. Remaining NUM/FOOD/WALL/SPACE/XP/KNOW/COOK/art still TODO.

### RAID packet — current uncommitted work after 8d94003

- RaidSettings snapshot/planner, owner API/UI editor, per-unit Enemy, waves,
  permanent compact RaidLaunchReceipts (details may expire after14days), saved
  presets8, explicit automatic vs manual, no silent boss downgrade in editor.
  Legacy automatic director uses the same config, restricted Validate(false).
- Adaptive coefficient .75–1.5: 2 easy +10%, heavy−18%; 10 recent records,
  actual participation, armour/health pool/energy readiness, dynamic cap up1200.
  Cancelled/manual/legacy incomplete fights excluded. Frozen announced stats;
  48 actor cap, spare budget spent on quality. PropertyDamaged outcome separated
  from heavy Lost. Legacy property-only Lost no longer forces2h rest.
- Settings control damage/speed/side/mixed species/tactics/waves/timers/retreat/
  loss limits and boss windup/cooldown/phase. Deferred wave now keeps duration.
  LootBudgets snapshot per storage/resource; only living escaping carrier takes
  remaining share, total event cap; mixed species loot uses actual killed kinds.
  Summary records contact/HP/heals/arrows/equipment/property/wall/adaptation.
- Read-only preview MUST NOT use WithVillageAsync: FinishAccountingAsync flushes
  unsaved tracked changes. New ReadRaidEditorAsync disposable context bypasses
  accounting; no SaveChanges/event/payment/model. Add palisade-completed preview
  regression to prove Ensure geometry is not persisted. This was found in review.
- Migrations: 20261017102505_RaidEditor0711 + RaidLossAccounting0711 + empty
  RaidParticipation0711 metadata sync. FIRST migration default RaidAdaptation
  explicitly contains Coefficient1 and empty lists (not generated empty{}).
- Full Domain480 PASS /tmp/patch0711-raid-domain-full.log. TypeScript full build
  PASS /tmp/patch0711-editor-ts5.log. Fixed previous rescue harness tsconfig node
  exclusion and fixtures import; full tsc-b now checked, not just app config.
- First API editor2/3 PASS; failed launch case was its test deleting AsNoTracking
  owned JSON via RemoveRange (unknown ordinal). Fixed fixture with ExecuteDelete;
  real launch/save/stop already succeeded. Broader repeat currently running:
  /tmp/patch0711-raid-api2.log (session77751), don't claim pass until inspected.
- First browser run could not find buttons: forgot built-harness Vite input,
  returned app fallback. Added raidEditor rolldown input; interrupted that failed
  run deliberately. Rerun4cases phone+desktop after restart. No UI pass yet.
- imagegen draft completed, source /workspace/generated_images/exec-3c022954-
  1730-4931-84fb-dff695f7f151.png, tool result functions.store key
  patch0711_fence_generation. References tiny-town + night/buildings-a.
  Output2172x724 differs requested1024x256, tiles inset/nonseamless. DRAFT ONLY,
  not yet packed/accepted/integrated. Need real atlas geometry and map/day/night
  inspection. Do not present the generated sheet as finished textures.
- Still finish raid validation/DB legacy migration/browser/preview regression,
  loot-share test, owner summary/director explanation. Then commit/push packet.
  NUM/FOOD/WALL/SPACE/XP/KNOW/COOK remain unimplemented, not dropped. Version
  files/runtime still0.7.9. No deployment request or installation for0.7.11.

- RAID final checkpoint: full Domain480 PASS plus15 focused (including4 added
  theft/participation/quality cases), broad API25 PASS. Final API4 PASS verifies
  exact idempotent settings conflict, pure preview even with a completed palisade,
  retained receipt after history expiry and legacy JSON upgrade/default1.
  Browser4 PASS phone+desktop; latest full TS PASS; lint no errors. Logs:
  /tmp/patch0711-raid-api-final.log, /tmp/patch0711-raid-browser2.log,
  /tmp/patch0711-raid-domain-last.log, /tmp/patch0711-raid-ts-final.log.
  Reviewed phone screenshot: inputs fit; select theme matched parchment afterwards.
  New editor lint warnings fixed (internal preset function, effect dependencies).
  Draft fence source copied to art/generated/fence-0-7-11 with explicit rejection
  of its atlas geometry; NOT integrated. Next implementation packet NUM/FOOD/SPACE.

### NUM / FOOD / SPACE checkpoint

- BuildingLabels.cs separate permanent registry keyed by unchanged global Number.
  Per-family1..N; unique type hides suffix via displayNumber0. Demolished registry
  entries retained; upgrade keeps identity; WoodPile/Woodyard etc share lineage.
  Village DTO/map/cards/lists/storage/Telegram use displayNumber; oldNumber intact.
- Additive migration20261017104253_BuildingDisplayNumbers0711 (32 total) has SQL
  backfill into NEW column only; never rewrites old JSON/Number/Sequence/resources.
  Actual PostgreSQL downgrade/re-upgrade test passed; new allocation continues both
  sequences and preserves owners/goods. Bot civic_notifications supports optional
  displayNumber0 and legacy fallback;14 focused tests PASS (3 new).
- Food1/3 finished cycles vs1/5; real CycleEngine15/30/36-cycle checks cost5/10/12,
  no idle/offline repeat. Existing origin/cost-basis/medical/manual meal guards kept.
  UI text no longer claims the existing Eat button is unnecessary; sources correct.
- Hut4/6/8 beds, Longhouse12/24/36, specialized upgraded stores120/resource,
  GreatGranary240. Hut upgrades now real staged/costed operations. Existing damage,
  coordinates, supplies/owners/housing assignments retained. NextKingdomArea shown
  using existing castle expansion, no moving walls or doubling full map.
- Shared UI upgrade predicate prevents unavailable actions in cards/private wallet.
  FullDomain492 PASS; API19 focused +3 initial including migration PASS;
  browser20 phone/desktop PASS; fullTS PASS. Initial fullDomain485/488 had3 old
  expectations (Longhouse20, Granary100, Hutunsupported); changed those tests to
  new intended capacities/availability; retained unsupported Library/maxHut cases.
- Logs /tmp/patch0711-label-food-domain-all-final.log, label-food-api-final.log,
  label-api.log, label-food-browser.log, label-ui-ts2.log, label-bot.log.
- Detailed choices/calibration: NUMBERING_FOOD_SPACE_0_7_11.md.
  Next: WALL progression/repair/navigation + accepted seamless day/night art;
  then XP/KNOW/COOK and full regression/package. Art draft still NOT accepted.

- NUM/FOOD/SPACE committed22b4164, bot notice changea1d153d, pushed feature branches.
  Next fence packet detailed entry/exit decisions saved in
  FORTIFICATIONS_0_7_11_IMPLEMENTATION.md. No fence code implemented yet; avoid
  catalog-only projects that cannot start while the main story project is active.
