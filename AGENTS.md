# D-087:0.7.7 READY, scheduled17Oct2026 07:30Oslo; NOT INSTALLED

Source44b4e426cdfdd9bb72621a9febeb624130a8d6e2, CI38979407726 SUCCESS439Domain+
251API+167browser/1normal skip,frontend/DockerPASS; PR51 merged6540b98 identicaltree.
Botd462e634/CI38980131645 SUCCESS890tests/serverSKIPPED; canonicalPR49 merged422cb81
identicaltree.24operator guards and local numbering SQLPASS. Read
 docs/dezertovia/RELEASE_0_7_7.md and PLAYER_FEEDBACK_0_7_7.md. Private ready
candidate/operator in bolig-agent ops/almighty-release. Earliest installation
17Oct2026 05:30UTC=07:30Europe/Oslo, one-time task scheduled. LIVE remains0.7.6/d0f88e2.
No post077 request yet. Fresh exact CI/baseline/services/hash preflight, actual
backups and separate independent check still required. Preserve progress/keys/
Propose/budget/quarantine; no old request replay, broad deploy, forced raids,
messages, payments, model probes or resets. Earlier INPROGRESS/HOLD are history.

# D-087: feedback fixes 0.7.7 IN PROGRESS, scheduled no earlier 17Oct2026 05:30UTC

Owner authorized implementation/testing and deployment at07:30Oslo17Oct.
Read docs/dezertovia/PLAYER_FEEDBACK_0_7_7.md. Stable building numbers, food,
targeted repair10%, selected storage, own housing, destination/stat labels,
free donation. Preserve coordinates/ownership/progress/resources/medical rules.
LIVE remains0.7.6/d0f88e2. No old rollout replay; exact CI/fresh preflight/backups
and independent check required. No installation before05:30UTC.

#0.7.6 INSTALLED and independently verified16Oct2026

LIVEgame d0f88e2e92d83e8c30a7566fb21c916a51023b1d, CI38969100973 SUCCESS424Domain+
244API+155browser/1normal skip,frontend/DockerPASS. PublicPR50 mergee62f516,
tree identical to tested source. Botmetadata891d1a8 CI38969173093 SUCCESS887tests/
serverSKIPPED, canonicalPR48 merge14e6dd5.20operator guards and4readonly parser
checksPASS. Freshpreflight38970195603, install38970339155 and independentcheck
38970711893 SUCCESS2026-10-16T21:38:28.7746706Z.119→119profiles,0progresslosses,
121accounts/loginbindings,25migrations,24frontendchunks,feedback0.7.6PASS.
PG/SQLite/code/config/profile backups saved;14protected botmodules/configs/
quarantine/Propose/budget preserved. No forced raids/messages/payments/model probes
or resets. Combat clicks now force medical refresh only for their own actor;
new injury/unavailability restrictions retained. Settlement yields after each
individually committed resident. Bounded internal-operation wait/hold metrics.
Old behavior4expected regression failures; fixed244API PASS locally and in CI.
Owner0.7.5 late stutter report showed lockmax3015ms/simulation median2ms. New
natural real-phone battle capture still required; not a claim all jitter gone.
See docs/dezertovia/RELEASE_0_7_6.md. Do not replay/reinstall older requests.
Earlier READY/HOLD/NOTLIVE records are history.

# D-086: late-fight contention after0.7.5, implementation in progress

Owner actual0.7.5 report: early smooth then late stutter. Simulation median2ms,
lock max3015ms; not server CPU proof. Attack click force-refreshes all residents
under global lock; narrow to actor only while preserving fresh medical authority.
Yield work settlement lock after each individually committed resident. Add bounded
operation wait/hold aggregates so future captures attribute holders/waiters.
Branchfix/combat-lock-contention, planned0.7.6, LIVE0.7.5/374323f unchanged.
Read docs/dezertovia/COMBAT_LOCK_CONTENTION_0_7_6.md; testing/CI/rollout pending.
No forced raids/messages/payments/model probes/resets or older request replay.

#0.7.5 INSTALLED and independently verified16Oct2026

LIVEgame374323f8c3ef519694497371716e611d3396ee43, CI38963553875 SUCCESS424Domain+
239API+155browser/1normal skip,frontend/DockerPASS. PublicPR49 mergedbbb5f29,
tree identical to tested source. Botmetadata380d8ea CI38963592945 SUCCESS887tests/
serverSKIPPED, canonicalPR47 mergede0a4cfa.20operator guardsPASS. Freshpreflight
38965661962, install38965752003 and independentcheck38966145677 SUCCESS
2026-10-16T20:59:15.5558416Z.119→119profiles,0progress losses,121accounts/bindings,
25migrations,24frontendchunks,feedbackversion0.7.5PASS. PG/SQLite/code/config/
profile backups saved;14protected modules/quarantine/Propose/budget preserved.
Subpixel combat motion, shared bounded jitter-aware pacing and wall damage-only
static-world fast path are LIVE. Reproduced9/48positions before vs48/48after,
late30/30,healthbar drift0 in actual map browser regression; not physical-phone
FPS proof. Natural next battle capture still needed; initial client frame spikes
and lock512ms were observed, do not claim every source of jitter gone. No forced
raids/messages/payments/model probes or resets. No old request replay/reinstall.
See docs/dezertovia/RELEASE_0_7_5.md. Earlier READY/HOLD/NOTLIVE entries are history.

# D-085: movement-only stutter after0.7.4; client pacing fix in progress

Owner reports map/buttons smooth, fighting avatars stutter. Two owner-provided
0.7.4 diagnostics: packet gaps1502/1609.5ms, frame updatep95~4–6ms; second capture
has early frame spikes117ms and world updatep95~41ms. Server full report peak
simulation23.7ms, lock512.8ms; read-only real-battle audit38960868199, no mutations.
Reproduced in actual renderer: slow fighter changes drawn coordinate only9of48
frames because world pixels are rounded; fixed1000ms tween ends before late
snapshots. Workfix/combat-motion-cadence, candidate0.7.5: keep subpixel actor
positions, common bounded jitter-aware pacing for avatars/enemies/healthbars;
ignore wall damage-only packets until geometry changes. No extrapolation, no
server/combat/medical/economic changes. Local26browser tests, TypeScript/lint/build PASS; exact CI and fresh rollout pending.
LIVE remains0.7.4/74adbe8 until exactCI/fresh rollout/independentcheck. No old
request replay/forced raids/messages/payments/model probes or profile resets.

#0.7.4 INSTALLED16Oct2026; independent verificationPASS

Owner requested no guard/post/camp bindings and combat diagnostics; prior server
fix installation authorization continued. LIVE game74adbe8c1d778f0751a755a4b36dbefe6f16a405,
CI38956420909 SUCCESS424Domain+239API+151browser/1skip,frontend/DockerPASS.
PR48 mergeddfdc0ac with identical tested tree. Bot metadata67f9149,
CI38957570632 SUCCESS887tests/serverSKIPPED; canonicalPR46 merged6ae84ce.
20operator guardsPASS. Preflight38958939581, install38959075262 and separatecheck
38959583714 SUCCESS2026-10-16T20:04:47.4389681Z.119→119profiles,0progress losses,
121accounts/bindings,25migrations,24publicJS chunks,feedback live versionPASS.
Old unfinished duties0.14protected botmodules/configs/quarantine/Propose/budget
preserved; PG/SQLite/config/code/profile backups saved. No forced raids/messages/
payments/model probes or resets. No guard task, sector, counter or camp leash.
Manual ground/target orders, local range-only self-defense, HP/ammo/heal receipt
preservation; slow navigation and combat renderer/snapshot fixes. Bounded server/
client diagnostics and Settings download enabled. No natural postdeploy raid
sample at finalcheck; physical phone FPS and any remaining freeze need actual
measurements. See docs/dezertovia/RELEASE_0_7_4.md and RELEASE_0_7_4_RESULT.json.
Do not reinstall/replay oldrequests. Earlier pending/NOTLIVE entries are history.

# New request: remove guard/post binding and diagnose combat freezes

Owner16Oct2026: players choose where to go/what to do; no guard duty. Implement
fix/free-combat-and-diagnostics, plan docs/dezertovia/FREE_COMBAT_AND_DIAGNOSTICS.md.
Manual movement/targeting, local self-defense, no camp leash or guard prerequisite.
Preserve paid work/HP/ammo/injuries; settle legacy paid duties once. Bounded
combat diagnostics and remove repeated citizen work from battle snapshot handling.
Implementation0.7.4 includes bounded server/client logs and renderer/navigation fixes.
Local424Domain+22browser PASS; final239API and exact CI pending. Not READY.
All code first, then focused checks and exact CI. Current LIVE0.7.3 below remains
until fresh rollout. No synthetic production actions or old request replay.

#0.7.3 INSTALLED16Oct2026, independently verified

Owner authorized installation. LIVE game672d72e167dd477f925df6394ecc5b188d3924ff;
CI38941941103 SUCCESS419Domain+235API+149browser/1skip, frontend/DockerPASS.
PublicPR47 merged9ac0007; its tree matches the tested source. Bot original civic
patch e4269ef7 plus version-registry fix f4dd269, CI38948020819 SUCCESS887tests,
serverSKIPPED. Narrow two-file bot payload preserves quarantine/budget/Propose.
Preflight38947072346 PASS. Initial install38947223449 switched runtime butFAILED
at feedback version lookup; separatecheck38947768084FAILED. Missing registry
mapping fixed in botPR45; repair38948536494PASS without game reinstall.
Final independentcheck38948699510SUCCESS2026-10-16T18:37:12.2283924Z:
119→119profiles,0skill/stat/renownlosses,121accounts/loginbindings retained,
25migrations,24publicJS chunks and feedback live-version lookupPASS.
PG/SQLite/code/config/profile backups saved; quarantine and Propose preserved.
No synthetic messages, model/payment/raid probes or progress resets.18operator
checks PASS, including version-registry coverage before switching future games.
See docs/dezertovia/RELEASE_0_7_3.md / RELEASE_0_7_3_RESULT.json. Do not reinstall
or replay older requests. Existing castle not moved; phone FPS not measured;
new director tax/economic executors remain separate work. Earlier PLAN below
is history. Current HUD is icons/bars, numbers on tap; Chat/Retreat/Heal44px row.

# D-081: реализация полного плана разрешена, без установки

16.10 владелец сказал «Делай» после DETAILED_OWNER_FIX_PLAN_0_7_2.md.
Реализовать весь сохранённый объём в feat/owner-combat-and-siege; код сначала,
затем общая проверка. Сохранять профили/прогресс/вложения, текущее Propose,
карантин/бюджет бота. Никаких production-волн, тестовых сообщений или установки.
Исторические PLAN ниже не блокируют разрешённую реализацию. Кандидат0.7.3, LIVE0.7.2.
См. docs/dezertovia/OWNER_CORRECTIONS_IMPLEMENTATION.md: код в проверке, не READY.
Первый стиль HUD отклонён владельцем; использовать существующие resource-card
рамки/фон/размеры, проверять на настоящей карте. Нельзя выдавать пустой стенд за игру.
D-083: только сердце/молния и полосы, числа по нажатию. «Здоров» убрать.
Уточнение владельца: боевые Отступить/Лечение в одном ряду с Чатом, высота44px;
без растягивания на экран. Тёмное недоступное лечение сохраняет видимую причину.
Постоянного HP вне боя нет, не подменять его100%; детали в DECISIONS/implementation.

# D-080: разбор новых замечаний после 0.7.2, только TODO

16.10 владелец прислал скриншоты: два выхода, окно врага, рывки игроков,
счётчик охраны, снос жилья, монастырь/замок/разрушение, повтор slug в URL.
Добавил компактные HP/силы и перенос Строить/Голосование в меню.
COMBAT-07: только рабочие Отступить/Лечение при личном участии в бою;
атака тапом, новую защитную стойку после уточнения о пользе исключить из пакета;
отдельная компактная строка без карточки врага. Отступление/лечение HP требуют
серверных состояний, сохранения HP при возврате и идемпотентного расхода.
HUD-03: владелец утвердил макеты телефона и ПК. Две тонкие полосы сверху:
красная HP, жёлто-зелёная силы; телефон между погодой и кошельком, ПК слева
от них, на ПК точные числа постоянно. Без новой строки/полноширинной панели;
стрелки макета не часть игры. COMBAT-07 — только личный бой, нет лекарства →
тёмная disabled-кнопка с причиной. Это утверждённый план, ещё не реализация.
Сверка скриншотов: в OWNER_FEEDBACK_0_7_2_REVIEW.md теперь отдельные12 строк
из трёх последних скриншотов +4 последующих уточнения и16 групп прежних жалоб.
Недострои/разрушение всех зданий/центральный замок/поражение/монастырь — часть
общего плана, не исчезнувшие пожелания. Старый релиз не равен свежей проверке;
перед завершением пакета проверить перечисленные регрессии, не обещать весь
план выполненным после одного HUD. Код/сервер этим поручением не меняются.
Подробная имплементация всего объёма: docs/dezertovia/DETAILED_OWNER_FIX_PLAN_0_7_2.md.
Сейчас PLAN; сервер/код не меняются. Рекомендации новых механик не выдавать за LIVE.
Читать docs/dezertovia/OWNER_FEEDBACK_0_7_2_REVIEW.md и NEXT_TODO.md.
Live04c3950/0.7.2 не менялся. Read-only audit38917722704: хижина4/4,
других свободных коек0, старая заявка сейчасrejected. Запрет сноса обоснован;
ошибка — скрытая причина/неясная заявка. Данные без имён в private ops.
Клик уже отправляет attack, но aside остаётся; сглаживание бойцов отдельно.
Сначала исправляющий пакет UI/снос/бой; полноценные руины, стартовый центральный
замок, проигрыш и роль монастыря пока проектирование, не выполненная механика.
Сохранить профили/прогресс/жильё/вложения; не выселять и не передвигать центр.
Документы не повышают версию и не разрешают новую установку. BotD079 отдельно,
не переигрывать старые rollout requests, не трогать модерацию/бюджет/Propose.

# D-077 /0.7.2 INSTALLED16.10.2026, независимо проверен

Owner «выкладывай» разрешил пакетD-074/D-075/D-076. LIVEgame
04c395052a106562b8e00b6c73a8a5b119b1aea5, bot five-file payloadac2ca8e4c8b53921cc34a47efa77ab046e376a0b.
ExactCI38891077915 SUCCESS401Domain+230API+133browser/1штатныйskip,
frontend/DockerPASS; bot38891110155 SUCCESS840tests/client/runner,
serverSKIPPED. PublicPR46 mergedd31c554, botPR40 merged8ef414a; деревья
совпадают с tested source. Дополнительный public mergeCI38892389532SUCCESS.
Freshpreflight38892398267PASS. Install38892470520 фактически заменил runtime,
APIhealthy11:18:41UTC, но workflowFAIL из-за ошибочного asset-probe: обрезал
точку в basename webworkerAll.es5-*. Диагностика38893024313FAIL, checker исправлен.
Independentcheck38893238465PASS. Повтор38893366537 поймал единичный404
BitmapFont, следующий полный публичный24chunk прогонPASS; bounded3attempt
retry404/502/503/504 добавлен, persistentfailure остаётся запрещённым.
Finalindependentcheck38893644226PASS, 2026-10-16T11:25:20.7425857Z.
Не переименовывать failed workflow в SUCCESS: факты и история в release record.

117→117профилей,0потерь навыков/характеристик/славы;
119аккаунтов и119Telegrambindings сохранены,24миграции.
PG/SQLite/обаconfig/snapshot backups сохранены. FlagsHospital/Almighty true,
Propose сохранён. Protectedidentity/Almighty/civic/budget/proxy hashes unchanged.
Публичные24JSchunk/0.7.2 и livefeedbackversion подтверждены; operator22testsPASS.
Нет синтетических сообщений, AI-проб, платежей/волн или сбросов.

Пакет: очередь/ошибки улучшений, компактныйHUD, пациенты в карточке больницы,
клик для атаки, reuse графики/плавное движение, контекст отзывов батюшки.
Обычным жителям ещё нужна охрана; самооборона всех TODO. FPS реального телефона
не измерен; не обещать устранение каждого возможного лага. Экономические/tax
инструменты Всевышнего в этот пакет не входят. Исторические hold/READY ниже
не текущее состояние. Не повторять install; новый exactsource только check
со снимками post-0.7.2. Нельзя запускать старыеpost071/live-combat requests.

# D-077: выпуск0.7.2 разрешён, проверяется точный пакет

Владелец16.10 сказал «выкладывай» для пакетаD-074/D-075/D-076.
Это снимает repository-only hold этого пакета. Следующий малый номер0.7.2;
package/lock/patch notes согласованы. Исходныйd732713 прошёл полный CI,
после изменения номера требуется новый exact CI. Не объявлять установленным
до actual preflight/backups/install/отдельного check. Узкий bot payload включает
новый feedback_context.py; защищённые budget/proxy/Almighty модули сохраняются.
Без синтетических сообщений, AI-проб, платежей/волн и сбросов прогресса.

# D-076: компактный HUD, пациенты и отрисовка боя — repository only

Владелец16.10: валюты съедают экран; пациент лежит снаружи больницы; вместо
четырёх боевых кнопок нужен клик по врагу; при врагах игра фризит, после них
нормальна. Читать docs/dezertovia/COMBAT_RENDERING_HOTFIX.md. В PR46 вместе
с D-074. Live0.7.1/e5de51e не менялся. Номер версии не повышать за эту ветку.
Исправлены подтверждённые лишние пересоздания объектов и рывки интерполяции;
не выдавать стенд за измерение FPS реального телефона или доказательство
отсутствия серверных задержек. Обычные жители пока атакуют на охране, царь
вступает Воеводой без неё. Всеобщая самооборона отдельно в TODO, не готова.

# D-074: queue capacity fix and read-only director diagnosis

Read docs/dezertovia/CIVIC_REQUEST_CAPACITY.md. Implemented in fix/civic-request-capacity.
Requests use active sites+pending applications, public3/private1; no legacy deletion.
Civic error text localized. No new deployment yet; LIVE0.7.1/e5de51e unchanged.
Director audit38875853709: enabled/Propose, recent none decisions,2budget skips,
no service failure. Hourly owner digest is an idea, not new active messages.

# D-073 /0.7.1 INSTALLED16.10.2026 и независимо проверен

Владелец явно разрешил исправление Telegram заявок и выпуск0.7.1 сейчас;
D-071/D-072 repository-only hold для данного пакета снят этим поручением.
LIVEgamee5de51ed04ff8f3b5cff1c5befb2032fa42635da, bot payload source7458b6bed439c19ce8be56db799911e1281f80d1.
Preflight38873012466 PASS; install38873092159 PASS; separate check38873661537 PASS.
Install verification2026-10-16T08:25:08.3536159Z, independent check2026-10-16T08:26:14.6780507Z.
116→116профилей,0потерь навыков/характеристик/славы;
118аккаунтов и118ExternalLogins сохранены,24миграции.
PG/SQLite/обаconfig/snapshot backups созданы. Hospital/Almighty true, Propose сохранён.
Game CI38871373656 SUCCESS387Domain+229API+125browser/1skip, frontend/DockerPASS;
botCI38871375837 SUCCESS828tests/client/runner, serverSKIPPED; operator22PASS.
PR45 mergedacfd0c3, botPR39 mergedcd653bd; runtime деревья равны tested source.

Настоящие заявки build/upgrade/demolish царю в личку, worker15с, до решения
не создаёт площадку. Bot OWNER_IDS + точная Telegram-привязка владельца,
повторное одобрение идемпотентно, обычные требования стройки проверяются снова.
Старая очередь27заявок принята; separate check подтвердил4реальные Telegram
квитанции Sent. Остальная очередь рассылалась постепенно; housing не входит.
Отчёты батюшке теперь сохраняют автора и код аккаунта только в личных карточках.
Установлены вместе7bot-файлов: app/db/feedback/feedback_identity/feedback_report/
almighty/civic_notifications. Установленный AI-budgetfix и town_proxy/llm/ai_budget
проверены по hashes и сохранены. Свечи/ключи/вещи/стройки/лечение не сбрасывать.
Нет синтетических сообщений, модельных проб, платежей или тестовых волн.

Новый операторpost071_release.py / post071-candidate.json, не старый rollout0.7.
Существующий exact source повторно НЕ устанавливать, только проверка со снимками.
Фактический факт — INSTALLED; старые ready/hold/candidate ниже являются историей.

# D-073:0.7.1 deployment authorized16Oct

Owner requested missing construction notices fixed AND0.7.1 installed now.
This supersedes D-071/D-072 repository-only holds for the combined release.
Read docs/dezertovia/CIVIC_TELEGRAM_0_7_1.md. New exact CI/payload/operator/backups/independent preservation
check required; never replay old0.7 trigger. No progress resets or synthetic
player messages. Actual pending civic notices to owner are authorized.

# D-072: идентификация автора отзыва, repository only

Владелец16.10 просит добавлять к багу/репорту батюшке автора и игровой код.
В feat/feedback-game-identity добавлен read-only /api/feedback/identity:
Telegram ID → существующий AccountId/CitizenId, прежняя bridge-авторизация.
См. docs/dezertovia/FEEDBACK_GAME_IDENTITY.md и парную bot-ветку. Без привязки
по нику, без изменения профилей/прогресса/свечей. Новая установка не разрешена.
Проверенный0.7.1/573af93 ниже — отдельный кандидат; новые изменения пока в ветке.
LIVE0.7/0540a2a не менялся. Номер за эту правку не повышаем.

# D-071:0.7.1 проверен в репозитории, НЕ установлен

Exact source573af93ccb2892b7e07c48529f8c387b63237cab, CI38864253995 SUCCESS:
387 Domain + 224 API + 125 browser PASS / 1 штатный skip; lint/build/Docker PASS. Public PR44.
Пакет: остановка активной/очередной нежити, Воевода/царь и команды, береговой
стык забора, управление старыми площадками/25% salvage первоначальному владельцу,
отступы/крестик, Dcoin/золотая иконка/личный wallet HUD. Питание автоматическое;
по жалобе об отсутствии кнопки уточнены тексты, алгоритм еды не менялся.
См. docs/dezertovia/RELEASE_0_7_1.md и POST_0_7_IMPLEMENTATION.md.
LIVE остаётся0.7/0540a2a. Новая установка НЕ разрешена; старые rollout/pins
не запускать. Все прежние pending/PLAN ниже — история.

# D-071: реализация пакета0.7.1 разрешена, проверка кандидата

16.10.2026 владелец после полного планаD-070 сказал «Делай». Реализация
в feat/post-0.7-fixes, по docs/dezertovia/POST_0_7_FIX_PLAN.md. Сверяться с
POST_0_7_IMPLEMENTATION.md. Сначала весь пакет, затем общая проверка.
Пакетный номер0.7.1; установленный LIVE0.7/0540a2a этим не меняется.
Работают API/Domain/React/Pixi основы; Dungeon служит образцом Воеводы/команд.
Новый запуск старого rollout0.7 запрещён. Установку этим поручением не начинать.
Сохранить частные материалы/владение, прогресс и лечение; без автоматического
сноса старых площадок. Самооборона всех жителей остаётся отдельной идеей.

# D-070: единый план0.7.1 и небольшие шаги версий

16.10.2026 владелец попросил полный план последних замечаний и запретил
ненужные скачки версий. Читать `docs/dezertovia/POST_0_7_FIX_PLAN.md`:
COMBAT-02A–D (берег/атака царя/крестик/усыпление), LAND-02 (старые личные
площадки и разборка), UI-06 (отступы), UI-07 (Dcoin/иконка/балансы на карте).
Dungeon — источник готового управления, царского Воеводы и проверки владения;
сохранять геометрию и устойчивое состояние деревенского RaidBattle.
Сейчас PLAN/docs only: реализация, изображения и новая установка не выполнены.
LIVE остаётся0.7/0540a2a; предыдущий выпуск D-069 уже завершён, не повторять.

Правило версий: следующий исправляющий пакет0.7.1, затем0.7.2 и далее;
после0.7.9 можно0.7.10. Документы/коммиты/тесты сами номер не повышают.
0.8 — осмысленный отдельный этап,1.0 — отдельное решение о готовности.
Текущий package0.7.0 пока не менять. Перед настоящим выпуском согласовать
один номер в package/lockfile/настройках/release metadata/патчноутах.
Самооборона всех жителей пока предложение, не утверждённое расширение.
Не сносить старые площадки автоматически; сохранять владельцев/вложения.
Тестовые и реальные свечи различать; Dcoin — переименование UI без конвертации.

# D-069 / 0.7 INSTALLED16.10.2026 и независимо проверен

LIVEgame0540a2a006d2c72b580b37f74387f1e53e68f9eb, version0.7/engine2.
Preflight38853486600 PASS; install38853561064 PASS; independentcheck38853958496 PASS.
APIhealthy05:04:48UTC, check05:05:31UTC, не раньше разрешённых05:00UTC=07:00Oslo.
113→113профилей,0потерь привязок/славы/навыков/характеристик;22миграции.
PG/SQLite/config/snapshot backups созданы. Telegram/health/routes PASS,
Hospital/Almighty true, Propose сохранён. Новый клиентский GamePage загружается.
Bot ONLYalmighty.py из3f7038c, SHA256748acaf68f5e1a739b2e0cb7e284e6206b0e5fcd69227deab7e81f1e23d1867f;
установленный AI-budgetfix сохранён, старые app/town_proxy/llm не копировались.
Synthetic messages/model probes/payments/forcedraids0. Не запускать старые deploy.
Оба game CI38817618664/38851717640 SUCCESS; bot38811556338 attempt2 SUCCESS816tests,
operator15PASS. READY/HOLD ниже — история, не текущее состояние.
Public release record: docs/dezertovia/RELEASE_0_7.md; private result:
deploy/LIVE_COMBAT_RELEASE_0_7_RESULT.json. Если exact source уже на сервере,
только check с существующим snapshot, повторная установка не нужна.

# D-069 — повторные проверки16.10 PASS, выпуск ещё не выполнен

По запросу «Перед выкладкой проверь не раз всё» повторный full CI38851717640
наТОМ ЖЕ0540a2a SUCCESS380Domain+217API+121browser/1skip, buildsPASS.
BotCI38811556338 attempt2 на3f7038c SUCCESS816tests, serverjobSKIPPED.
Оператор15PASS и требует predeploy_ci38851717640 вместе с первичным38817618664.
Подробности в docs/dezertovia/RELEASE_0_7.md и приватном PREDEPLOY_RECHECK_0_7.md.
Candidate.ready=true после второго успеха; LIVE61d726c не менялся. Выпуск только
16.10.2026 07:00Europe/Oslo=05:00UTC; actual backups/report/deploy/independent
preservation check обязательны. Не выдавать проверки за фактическую установку.

# D-069: 0.7 READY, scheduled16.10.2026 07:00 Europe/Oslo

Full code/checks completed. Public PR37 MERGED3e757dae87a4f3984b26f71104c7994abb0b06a9,
tree identical to tested runtime0540a2a006d2c72b580b37f74387f1e53e68f9eb.
CI38817618664 SUCCESS:380Domain+217API+121browser/1expectedskip; frontend/DockerPASS.
Bot source3f7038c886e8ea37c98191a32ba0c3f558b05e69, CI38811556338 SUCCESS816tests;
PR38 merged canonical0054f4c. Payload ONLYalmighty.py over installed budgetfix3fd0c1c.
See docs/dezertovia/RELEASE_0_7.md for final scope/pins/readiness, and implementation
log for history. Includes AI-SPEECH-01: no director service JSON in Father chat.

INSTALLATION NOT YET DONE. LIVE0.6.1/61d726c verified externally HTTP200, Telegram
login true/dev false. Scheduled one-time task confirmed16.10.2026 07:00Europe/Oslo
=05:00UTC; no earlier. Private bolig-agent ops/almighty-release contains reviewed
live-combat operator/candidate/payload. Trigger live-combat-request.json must NOT
exist before deadline; report→deploy→independent check, exact successful CI, backup,
profile/skill/stat preservation. Do not run old request/pins or private main deploy.
No synthetic messages/payments/raids, no progress reset. Append actual deployment
receipt only AFTER release. Earlier checkpoints/PLAN/CI failures below are history.

# D-069 / 0.7 candidate: implemented, final CI then scheduled release

Full D-068 code batch and AI-SPEECH-01 are implemented. Public PR37 contains
server individual battles, real terrain/gates/breaches, serial waves and combat
UI, energy/cycle/boundary/storage fixes. See docs/dezertovia/COMBAT_AND_FIXES_IMPLEMENTATION.md
for exact code/checkpoints. Bot PR38 merged, tested source3f7038c / CI38811556338
SUCCESS816 tests; only almighty.py runtime payload, preserving installed budget
repair3fd0c1c. Previous game CI passed but final game source needs exact CI.
LIVE game remains0.6.1/61d726c. Automatic release scheduled16.10.2026 07:00
Europe/Oslo =05:00UTC, NEVER earlier. Private ops candidate may be prepared now;
do not create deploy/live-combat-request.json until the deadline. Release only
ready exact pins with successful CI, backups, independent preservation check.
No artificial player messages/raids/payments, no progress or stat resets.

# D-069: реализация разрешена; установка не раньше07:00 Осло

Владелец15.10.2026 разрешил весь код и тесты пакета D-068. Реализовать в
feat/live-village-combat: COMBAT-01/UI, ENERGY-02, UI-05, CYCLE-02, ENERGY-03,
LAND-01, STORAGE-UX. Сначала закончить код, затем проверки. Установка разрешена
на16.10.2026 07:00 Europe/Oslo (05:00UTC), НЕ раньше. До времени LIVE0.6.1
не менять. Разрешение на реализацию снимает прежний PLAN-only, но не временной
барьер. Не устанавливать непрошедший точный CI код, не сбрасывать прогресс/
реальные свечи, не запускать тестовые волны/сообщения на production. См.
docs/dezertovia/COMBAT_AND_FIXES_IMPLEMENTATION.md. Никакая установка ещё не сделана.

# D-068: единый план патча — новая оборона и связанные исправления

Владелец: «Пиши сейчас весь этот план, будем все править». Состав/порядок/
приёмка: docs/dezertovia/COMBAT_AND_FIXES_PATCH_PLAN.md. Вместе D-066 новая
боёвка и боевой UI; ENERGY-02, UI-05, CYCLE-02, ENERGY-03, LAND-01; компактная
STORAGE-UX без смены ёмкостей. Директор один, костёр ориентир, состав по готовности,
отдельные серверные враги, вход/пролом, автоатака/тап. Полная экипировка/рюкзаки/
эмбиент/рулетка — следующая очередь. Сначала согласованный пакет кода, потом
общие проверки. Сейчас создан план, код/сервер не менялись. LIVE0.6.1 остаётся.
Для начала реализации читать этот план и последние сообщения; не повторять
старые установки и не выдавать PLAN за deployed. Номер нового выпуска не назначен.

# D-067: сегодняшняя предложка сверена с0.6.1, только TODO

Прочитаны все88 записей приватного feedback-report38803828120,34 за15октября,
#55–88;11 новых после прежней сверки (#78–88). См.
`docs/dezertovia/FEEDBACK_REVIEW_2026_10_15_EVENING.md`.
Первый приоритет: ENERGY-02 (#87: усталость до reload после регенерации) и
UI-05 (#88: глаз перекрывает сообщение), оба после0.6.1. CYCLE-02/#82,
ENERGY-03/#78 и LAND-01/#86 — проверка/воспроизведение, не объявленные готовые
исправления. Упоминания/передача/камыш/12ч regen/ники/основной запрет личной
стройки внутри уже есть. Новые идеи не автоматически одобрены. Сейчас docs/TODO,
никакого deploy/сброса/сообщений/пометки отзывов в боте. D-066 остаётся PLAN.

# D-066: план живой обороны и адаптивных нападений

Владелец просит сохранить обсуждение, сейчас только PLAN. Читать
`docs/dezertovia/RAID_REWORK_PLAN.md`: одна выбранная Всевышним сторона,
отдельные враги/HP/удары, проход/пролом, автоатака и выбор цели, состав по
готовности деревни. Костёр только ориентир центра, НЕ цель уничтожения.
Один ИИ, тактические скрипты, существующий Propose/одобрение сохранены.
Параметры и политика ущерба отмечены как предложения. Старый первый этап
«одна пачка + общий расчёт» не удовлетворяет новому запросу. LIVE0.6.1 неизменён;
разрешение D-064 на его выпуск не разрешает новую боёвку. Реализацию не начинали.

# D-064: 0.6.1 installed and independently verified

15.10.2026 владелец: «Все выкладывать». D-063 repository-only hold снят для
полного проверенного патча. LIVE game61d726c184315d54c3d2c86b4984954bf818e31b,
bot6e87f6054ab6700f552f6d8fe2c56a3b41ee33d6. CI38795831107 PASS:
349 Domain+207 API+107 browser/1 штатный skip; frontend/Docker PASS.
Bot CI38792441921:801 PASS. Установка38797652832 и независимая проверка
38798016055 PASS:111→111 профилей,0 потерь прогресса,21 миграция, PostgreSQL/
SQLite backup. Всевышний Propose сохранён, Telegram auth работает. Реальные
свечи/ключи не менялись, синтетических сообщений/платежей не было.
Посадник подаёт заявки на любые доступные здания; одобряет царь. Четыре bot
файла, включая llm.py, установлены. См. docs/dezertovia/RELEASE_0_6_1.md.
Старые candidate/hold ниже — история. Не повторять установку по старым заданиям.

# D-063: candidate0.6.1 — menus, energy, construction and AI diagnostics

Текущее поручение15.10: docs/dezertovia/MOBILE_NEEDS_0_6_1.md. Только кандидат
в репозитории, без новой установки. LIVE0.6 изD-062 остаётся. Энергетик300ДЗК/
3тестовыхсвечи,12чобщийоткат; взвар4–16/120–480мин только с одобрением царя.
Критичный дефект EF JSON/повторного Save и rollback чужой стройки исправляется
с проверкой сохранности. Для bot нужны almighty.py И llm.py, старый payload
не подходит. Прогресс/реальные свечи не сбрасывать и не менять.

Дополнение D-063: посадник отправляет заявки на любое доступное здание
(включая больницу/замок), царь утверждает; до решения нет площадки/резерва
деревьев. Повторная полная проверка при одобрении. См. раздел дополнения в
MOBILE_NEEDS_0_6_1.md. Всё ещё repository-only, LIVE не менять.

# D-062: 0.6 released and independently verified

15.10.2026 D-061 выполнено. LIVE0.6, game143bf9ad569d2bc01d43356c8fc21467c59e544a,
bot9a9c8c27233244a9e0e9a709e4a2ff1f12ad0374. CI38784399850 PASS:
346 Domain+196 API+91 browser/1 штатный skip; frontend/Docker PASS.
Install38785792763 и independent check38786456457 PASS:110→110 профилей,
0 потерь прогресса,19 миграций, PostgreSQL/SQLite backup. Telegram/больница работают.
Всевышний включён Propose; первый естественный анализ модели успешен, эффекты
требуют согласия царя. Энергия12 реальных часов при100% условиях; предупреждение
об обмороке и понятный таймер. Нет сброса/настоящих списаний/синтетических сообщений.
Game PR34 включает PR33 и слит в main; bot PR35 слит в свою каноническую ветку.
См. docs/dezertovia/RELEASE_0_6.md. D-058 отменён D-061. Старые PLAN/LIVE ниже — история.
Не повторять старые установки. Новое runtime требует нового задания, docs не деплоить.

# D-061: approved energy adjustment and 0.6 rollout

15.10.2026 владелец: «Добавь эти правки, если наш патч готов для выгрузки то запускай».
Применить восстановление полного запаса за12 реальных часов при100% условиях,
предупреждение перед третьим уставшим действием; без бонуса25% после обморока.
Срок обморока12 игровых часов, расход/медленная добыча/стройка сохранены.
Разрешена выкладка готового проверенного0.6 с backup, совместимым bridge бота,
сохранением прогресса и включением Всевышнего в Propose по D-056.
Это новое поручение снимает D-058 о запрете установки. Не сбрасывать данные,
не менять настоящие свечи/налоги, не делать синтетических Telegram сообщений.
Точный SHA/CI и итог установки записать в RELEASE_0_6.md и приватный release record.

# D-060: вся очередь реализована в ветке

15.10.2026 «Делай все правки»: восемь пунктов TODO_EXECUTION_GUIDE.md реализованы
в feat/kingdom-economy поверх e7fbe91 / PR33. Читать
docs/dezertovia/TODO_IMPLEMENTATION_0_6.md: код, тестовые числа и проверки.
Полный итоговый CI — на публикуемом SHA; старые PLAN ниже исторические.
Налог preview, свечи тестовые, прогресс сохранён. Бот не изменялся.
D-058 действует: НЕТ server install/enable/настоящих Telegram сообщений.

# D-059: инструкция выполнения TODO

15.10.2026 по просьбе владельца подготовлен docs/dezertovia/TODO_EXECUTION_GUIDE.md:
восемь пунктов, файлы, порядок, приёмка; ВСЁ под 6.1 SOL Высокое. Это только инструкция,
не поручение начать все новые функции. Новое runtime не менялось.
D-058 ниже продолжает запрещать server deploy/enable/настоящие сообщения.

# D-058: СТОП ВЫКЛАДКЕ

15.10.2026 владелец сказал «Пока не выкладывать». Продолжать код/проверки/
GitHub, НЕ устанавливать game/bot, НЕ включать Всевышнего на сервере и НЕ
отправлять настоящие owner proposals. D-056 разрешает реализацию, но его
часть о выпуске/включении отложена этим новым решением. LIVE0.5.3 не меняется.
Game PR33, bot PR35; только CI/репозитории до нового поручения.

# D-056: реализовать и включить Всевышнего

Историческое поручение D-056: реализовать весь проект и режим Propose.
Включение на сервере ОТЛОЖЕНО решением D-058 выше.
Один ИИ анализирует, присылает владельцу через батюшку предложения с кнопками
согласия/отказа. Это явно разрешённые личные уведомления, не групповой флуд.
Минута на дешёвую проверку,15мин на модель, важные события не чаще5мин.
Сначала весь код, затем итоговые проверки/выпуск с сохранением прогресса.
Реальные налоги/кошельки не менять. Старые PLAN/Off не отменяют новое поручение.

# D-055: подробный план имплементации Всевышнего

15.10.2026: владелец просит план. Основная техническая точка:
`docs/dezertovia/ALMIGHTY_IMPLEMENTATION_PLAN.md`. Один ИИ, общая память,
инструменты/режимы/lease/ledger/scheduler/чат/API/приёмка/включение.
Директор остаётся PLAN; код и сервер этим поручением не меняются.

# D-054: Всевышний — один ИИ

15.10.2026: владелец уточнил: бухгалтерия, вражеское командование, сложность,
ивенты и прочее — обязанности ОДНОГО директора, не отдельные ИИ/агенты.
Единые личность/память/замысел/журнал, серверные модули исполняют решения.
ALMIGHTY_DIRECTOR_PLAN.md обновлён; существующий runtime не менялся.

# D-053: таймер нападения и срок лечения

15.10.2026: исправить преждевременную ходьбу зомби/скелетов и огромные числа
минут лечения. До strikesAt — только предупреждение, затем показ/атака;
серверные сроки волн/боевой расчёт не менять. Сроки лечения в реальных днях,
часах, минутах с явной подписью; не пересчитывать без реального cycle config.
Изменения в feat/almighty-patchnotes / PR33, без deployment. Проверки и детали:
docs/dezertovia/RAID_MEDICAL_FIXES.md. LIVE остаётся прежней0.5.3.

# D-051/D-052: Всевышний и история обновлений

15.10.2026: директор жизни/сложности/ивентов — PLAN в
`docs/dezertovia/ALMIGHTY_DIRECTOR_PLAN.md`. Объединить командира, бухгалтера и
батюшку; сервер валидирует ограниченный каталог. Бухгалтер только советует,
чат не даёт прав; числа предложены, автомат не включён.
D-052: история по нажатию версии в настройках, без deployment.
Ветка feat/almighty-patchnotes; существующая0.5.3 остаётся LIVE.

# D-050: 0.5.3 выпущена и проверена

15.10.2026 выполнено D-049. LIVE0.5.3, runtime sourcea980d8daeb2d3ff10609df7894444d57c8992c74.
PR32 слит в main (merge724484d). CI38736161629:329 Domain +180 API,
71 browser PASS/1 штатный skip; frontend/Docker PASS. После установки100→100
профилей, без потери личности/славы/навыков/опыта;17 миграций, больница100
подтверждений/0 недоступных, Telegram работает. Никакого сброса/тестовых оплат
или сообщений. Закрыты UI-01–04, BUILD-01 и ENERGY-01 (уточнение правил/цены).
Документ: docs/dezertovia/RELEASE_0_5_3.md. Исторические PLAN/LIVE ниже устарели.
Не повторять старую0.5.2 выкладку/активацию. Новые идеи остаются backlog.

# D-049: выполнить исправления и выложить проверенный выпуск

15.10 владелец сказал «Правь и выкладываем». Разрешены реализация D-048,
итоговые проверки, GitHub и затем deployment с резервными копиями/сохранением
прогресса. Ветка fix/post-release-regressions. Старые «только план» D-046–048
перекрыты. Не менять сложность/тарифы/очередь, не сбрасывать данные.
Сначала весь код, затем проверки. Версия исправлений0.5.3.

# D-048: подробный план исправлений, пока без реализации

Владелец попросил составить план для следующего захода.
Главная точка: docs/dezertovia/REGRESSION_FIX_PLAN_0_5_2.md.
Порядок: весь UI-пакет → серверные резервы стройки → энергия/тексты → итоговые
проверки. UI-01–04, BUILD-01, ENERGY-01. Сложность/очередь/прогресс сохранять.
При госпитализации резерв нельзя освободить без безопасного правила возврата
оплаченного цикла; исходно держать вместе с паузой. Старые циклы не терять.
Сейчас только docs, без кода/тестов runtime/выкладки. 0.5.3 лишь предложение.

# D-047: проверить UI-регрессии и отзывы, без нового выпуска

15.10 владелец сообщил о чате поверх здания, характеристиках супруга, отступе
подписей и попросил прочитать отзывы бота. Расследование/TODO:
docs/dezertovia/FEEDBACK_TRIAGE_2026_10_15.md. 77 записей прочитаны, новые53–77
разобраны; UI-01–04, BUILD-01, ENERGY-01. Код игры/сервер не менялись.
В приватном ops-репо добавлен вывод уже отредактированных текстов без имён/ID
пользователя: хранилище артефактов заполнено. Это чтение, не deploy/Telegram.
Не считать PLAN исправлениями и не принимать ускорение/ослабление правил из отзывов.

# D-046: карточка персонажа — расследование и TODO, без реализации

15.10.2026 владелец попросил найти причину повторов «Супруг» среди характеристик
и записать в TODO. Подтверждено: CharacterSheet.tsx:119–124 повторяет блок
супруга/жилья/состояния внутри citizen.stats.map; числа берутся из своего citizen.
План P1 и приёмка: docs/dezertovia/NEXT_TODO.md, D-046. Пока не исправлено.
Это поручение не разрешает новый deploy или изменение игрового кода.
Последний выпуск0.5.2: tag v0.5.2, source29e4ba057ea903e6ef827af64892c599c16818b9;
исторические записи ниже о кандидате0.5.2/старом LIVE0.5.1 устарели.

# D-043: выпуск0.5.2 разрешён

15.10 владелец: «Загружаем обнову на сервер чтоб народ начал тестировать».
Разрешены согласованный bot/game выпуск, резервное копирование, additive
миграции и включение больницы после установки адаптера. Не сбрасывать прогресс,
не выполнять тестовые платежи или сообщения в Telegram. Перед изменениями
read-only проверка; после точные SHA, proxy/auth/health и базовая сохранность.

# D-042: пациенты лежат, раненых переносят после боя

В ходе реализации владелец уточнил: персонаж должен лежать в больнице с причиной
лечения; добавить переноску раненых после боя. Код в GameService.Rescue,
RescueRules, VillageWorld, WoundedTransport migration. Другой игрок переносит
без профессии, не лечит; fallback санитары через5 реальных минут, до постройки
больницы временный лазарет у костра. Native InjuredUntil и duel Recovery раздельны.
Offline пациенты видны, поза лёжа, причина+таймер. Ряд коек отвергнут: разнесённые
места на карте (до4, свой всегда) и отдельные полные карточки всех в больнице.
Кандидат0.5.2, проверки/состояние в IMPLEMENTATION_LOG.md. Пока только РЕПО.

# D-041: весь пакет разрешён к реализации, без deployment

15.10 владелец сказал «Выполняй наш полный план». CURRENT_MASTER_PLAN/BALANCE_PROPOSAL
теперь рабочий объём. Все18 блоков + крупные undead; ветки feat/cooperative-update
игры и бота. Сначала весь код, затем итоговая проверка/тесты. Новые числа baseline
применять с сохранением прогресса, без live/реальных списаний. Не останавливаться
между блоками и не трактовать старую запись «только план» как запрет текущей работы.

# D-039: начать с актуальной сохранённой памяти

Главный текущий план: docs/dezertovia/CURRENT_MASTER_PLAN.md. Продолжение после
сжатия: SESSION_HANDOFF.md; цифры: BALANCE_PROPOSAL.md; порядок: BATCH_UPDATE_PLAN.md.
18 блоков общего пакета ещё не реализованы. Сейчас владелец попросил сохранить
план, не начать код/deployment. Бот-часть tone PR33 уже LIVE, больница/обводки/
удалённые кнопки только готовы в отдельных PR. Старые TODO не переопределяют это.

# D-037: пакет правок, тесты после завершения кода

Следующее поручение на реализацию по BATCH_UPDATE_PLAN.md выполнять единым
согласованным пакетом: сначала закончить правки, затем общий поиск ошибок и
тесты. Не гонять полный набор после каждой кнопки/мелочи; исправления проверять
целево, полный повтор по риску. Не отключать обязательный финальный CI и не
проверять сохранность ПОСЛЕ изменения live. Сейчас только план, не реализация.

# D-027: восстановление маршрута игры после обновления бота

14.10.2026 владелец сказал «Чини». Бот из старой feature-ветки затёр proxy,
из-за чего адрес игры открыл legacy frontend, хотя .NET upstream был здоров.
Маршрут восстановлен, bot source получил preflight/health guard (PR31).
Deployment38603201121 и final check38603465467 успешны. Игра остаётся0.5.1,
release ad88be344908a7fb42bc434ce89aad94c92d1665. Без сброса/новой механики.
Старые bot-ветки перед deployment rebase: в них guard ещё отсутствует.
Больница отложена владельцем на время инцидента, не отменена.
Подробности: docs/dezertovia/INCIDENT_ROUTING_2026_10_14.md.

# D-026: небольшие бонусы восстановления (только план)

Уровень больницы + личный навык пациента сокращают срок. Рекомендации процентов
не утверждены. Снимок один раз при поступлении, общий authoritative срок у бота;
цены и часовые эффекты платного лечения сохраняются. См. HOSPITAL_PLAN.md.

# D-025: больница — сначала план и графика

Владелец выбрал больницу следующей задачей. Подготовить HOSPITAL_PLAN.md,
найти отдельный hospital asset; при отсутствии нарисовать день/ночь. Это ещё
не game integration/списание/выпуск. Графика art/hospital-proposal — предложение,
нормализовать/запечь обводку перед runtime. Сохранять native Injury/Remedy,
дуэльный срок отдельно; оплата у бота, с durable idempotency. Без deployment.

# D-024: обновить TODO, без изменения игры/сервера

Последние мысли владельца сравнить с кодом; актуальный порядок/статусы в
NEXT_TODO.md и VIEWER_BACKLOG.md. Энергия/энергетики уже есть частично,
больница у бота есть; не дублировать. Мост — жалоба для воспроизведения.
Замок поднят в ближайший план. Срок обморока игровые часы, дуэли реальные.
Тарифы/свечи дуэли из действующего сервиса бота. Это только план, без списаний.

# D-022: компактная кнопка чата (0.5.1)

Убрать полноширинную полосу и население из чата; жители/онлайн наверху «Город».
Чат/уведомления не перекрывают голосование и другие окна. Сохранять SignalR,
историю, черновик, режимы и счётчик непрочитанных. Без изменения данных игроков.

Версия текущего выпуска0.5.1 по решению владельца. Источник номера —
web/package.json (0.5.1); SettingsPanel отображает0.5.1. Обновлять номер и
release record вместе при следующих выпусках.

# D-021: режимы чата и уведомления

Владелец сказал «Приступай»: скрытый/четверть/full, удобные кнопки, toast как0.4.
Сохранять существующий SignalR, историю/отправку/события/NPC и оформление.
Сверяться с CHAT_MODES.md. Без сброса/свечей и manual сообщений игрокам.

# Поручение D-020: реализовать пилот стройплощадки хижины

Владелец сказал «Делай» после подробного плана CONSTRUCTION_VISUAL_PLAN.md.
Подключить стадии хижины, сохранить исходники, материалы сбоку и ночной фонарь.
Серверные стадии/время/стоимость и прогресс не менять. Проверить до выпуска.

# Поручение14.10: ночные здания, чат и мобильные исправления

Владелец явно разрешил подключить все26 подготовленных пар зданий день/ночь
с плавным переходом и вернуть Срамославу общий игровой чат. Исправить скриншоты
Telegram: описание поручения во всю ширину над кнопками; ввод количества не
сбрасывать при стирании/обновлении склада. Сверяться с NIGHT_BUILDINGS_CHAT.md
и D-019. Старый остановленный художественный блок не запрещает это поручение.
Не сбрасывать игру, не менять реальные свечи и не посылать сообщения в Telegram.

# Текущее уточнение: один мир и Срамослав

Владелец временно оставил только Дезертовию: скрыть выбор/создание городов,
ограничить другие миры конфигурацией, сохранить их код/данные для возврата.
Упростить Telegram вход, убрать сломанный widget с нашего экрана и полностью
выключить вход по почте в deployment (D-017). Телефонные кнопки по реальному числу.
Заменить Седого на Срамослава и подключить существующий ИИ бота для беседы
в игре. Узкий private bridge, без game commands/денег/сообщений в Telegram.
Эти новые явные решения имеют приоритет над историческим сохранением UI миров.

# Текущее поручение14.10: первый запуск

Владелец разрешил по очереди начать адаптацию с Telegram-авторизации и запуска
обновлённой игры на своём сервере, включая переключение нынешнего адреса.
Сохранять все функции автора. Проверять отдельно перед переключением;
старую базу не сбрасывать, реальные свечи/другие игры не менять.
Документальные ограничения старых этапов ниже не отменяют это поручение.

# Инструкции для работы над Дезертовией

Начинать с docs/dezertovia/README.md и DECISIONS.md; найти нужные DZ-ID в
RULES_AND_SOURCES.md и ADAPTATION_MATRIX.md, затем читать реальные функции.
Последнее явное решение владельца имеет приоритет над историческим backlog.

- Инженерная основа автора — .NET Domain/Infrastructure/Api, EF/PostgreSQL,
  React/PixiJS/Vite/SignalR. Адаптировать, не переписывать проект и не ставить
  старый Python-двигатель вторым владельцем игрового состояния.
- Сохранять сервисы, каталоги, lifecycle, auth, ORM, renderer и тесты основы.
  Исправлять параметры/правила и добавлять недостающие поля/модули точечно.
- Новый художественный блок остановлен; существующий UI/редактор автора остаётся.
  Не импортировать старые shell.js/Canvas renderer и не рисовать без нового поручения.
- Все функции автора сохраняем доступными, включая новые набеги/данжи/миры.
  Наша Дезертовия — основной мир; медленная работа, профессия2ч,
  queue1+1, общий запас внутри/свой снаружи, последнее решение царя.
- Основные свечи, донаты, модерация и другие игры бота отдельно. Экономика
  Дезертовии тестовая; налог preview, AI read-only. Права решает сервер, не модель.
- Документы различают LIVE старой версии, BASE, BRANCH, PLAN, LATER, PAUSED/EXCLUDED.
  Нельзя выдавать найденный/написанный код за проверенную и выпущенную функцию.
- Реальные ключи/.env/БД/бэкапы/сырые отзывы/рабочие cookie не включать в git,
  Actions artifacts, публичные планы и вывод команд. Не давать deployment ключи
  сторонней ветке. Все upstream изменения принимаются выборочно после проверки.
- Работать в отдельных ветках. Не force-push upstream, не зеркалировать поверх
  своих изменений. Выпуск/сброс/переезд live не следуют из поручения написать план.
- Для каждого этапа обновлять decisions, матрицу и progress.json вместе с кодом;
  запись реальной проверки обязательна перед verified/released. AGENTS.md и
  CLAUDE.md сохранять согласованными. Коммиты на английском, без сторонних подписей.

Историческое поручение до запуска: документация без изменения кода. Перекрыто текущим поручением Telegram/Oracle выше.

Обновление контекста14.10: перед прежней матрицей читать
docs/dezertovia/CURRENT_ADAPTATION_LIST.md. Отдельно просмотренe23bc94,
слит в текущую release-ветку. Chat/Presence/Governors/боевые системы
теперь существуют; не создавать их повторно. По D-013 сохранять их доступность,
адаптируя правила, вместо отключения. По D-014 чат0/¼/full — явная задача UI.
Первый этап Telegram/Oracle выпущен, сверяться с TELEGRAM_LAUNCH.md. Другие правила/UI ещё адаптируются по очереди.

Последние решения: D-013 — НЕ отключать новые функции автора; D-014 — чат
по нашему образцу (скрытый со всплывающими/четверть/fullscreen). Сверяться с
CURRENT_ADAPTATION_LIST.md. Сохранение инженерии включает сохранение возможностей.
