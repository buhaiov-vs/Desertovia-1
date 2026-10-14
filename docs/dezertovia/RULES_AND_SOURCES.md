# Правила, состояния реализации и источники

## Как читать статусы

- **LIVE** — есть в последней подтверждённой старой версии 0.4.2 (commit из README).
  Это не утверждение свежего состояния сервера или реализации в новой основе.
- **BRANCH** — подготовлено в TG-bots 0.4.3, не выпущено.
- **BASE** — найдено в существующей C#-основе; конкретные параметры могут отличаться.
- **PLAN** — согласованное направление или задача, детали ещё выбирать.
- **LATER** — пожелание на будущее, не разрешение вводить сейчас.
- **PAUSED / EXCLUDED** — временно отменено / противоречит текущим решениям.

Пути `sramoslav/...`, `assets/town/...`, `tests/test_town...` ниже относятся к
репозиторию TG-bots и должны читаться на правильном коммите. Они не являются
файлами данного репозитория. Приватный исходник не переносить в публичные документы.

## Карта старой реализации: что именно искать

| DZ-ID | Область / состояние | Источник старой логики | Данные / доказательство |
| --- | --- | --- | --- |
| DZ-01 | Одно царство, сервер решает результат / LIVE | `sramoslav/town_engine.py`: `tx`, `start`, `advance`, `snapshot` | `town_world`, `town_players`, `town_jobs`, уникальная активная работа; `tests/test_town.py` |
| DZ-02 | Telegram и игровой доступ / LIVE | `sramoslav/town.py`: `allowed`, `post`, `csrf`; engine login/session/approve | `town_sessions`, `town_login`; `tests/test_town.py`, `tests/town_browser.py` |
| DZ-03 | Профессия раз в 2 реальных часа / LIVE | engine `role`, `ROLE_COOLDOWN`, features `ROLE_NAMES` | role, role_until; повтор той же роли не продлевает срок; `tests/test_town_features.py` |
| DZ-04 | Сроки, энергия и добыча / LIVE | engine `DURATIONS`, `HARVEST`, `ENERGY_COST`; features `parameters` | Начатая работа хранит start/end/груз; не пересчитывается новым балансом |
| DZ-05 | Поручение заканчивается офлайн, результат однократен / LIVE | engine `advance`, features `finish`, `record`, gateway цикл обработки | status, payload, ledger.key; профильные тесты завершения/возврата |
| DZ-06 | Одно текущее + одно следующее / LIVE; точный участок / BRANCH | features `action` queue, `queued`, `run_queue`; gateway обработка queue | `town_queue` kind/project/deposit/error; `tests/test_town_features.py`, `tests/town_queue_browser.py` |
| DZ-07 | Сотрудничество и рабочие места / LIVE | engine `crew`, `start`, `work_slots`, `snapshot`; features ограничения | seen, роль, запущенные задания и резерв оставшейся работы |
| DZ-08 | Запасы, вместимость, резерв в пути / LIVE | engine `capacity`, features `get`, `add`, `capacity`, `reserve` | `town_stock(scope,resource)`; объём нельзя обойти одновременно принятыми заданиями |
| DZ-09 | Конечные участки / LIVE | `sramoslav/town_deposits.py`: `DEPOSITS`, `pick`, регенерация | `town_deposits`; `tests/test_town_deposits.py` |
| DZ-10 | Доставка как помощь без смены роли / LIVE | features `start` courier, `build_materials`; realm `path` | Переезд реального груза/резервов, не создание материала |
| DZ-11 | Общие стройки и порядок открытия / LIVE | engine `PROJECTS`, `available`, `built`; bridge work | `town_sites`; мост и деревянный забор параллельно, стены после забора |
| DZ-12 | Уровни 1–3, подготовка и ремонт / LIVE | `sramoslav/town_development.py`: `plan`, `start`, `finish`, `wear` | `town_development`; `tests/test_town_development.py` |
| DZ-13 | Производства / LIVE | features `RECIPES`, `task_available`, `start`, `finish` | Материалы резервируются при начале; feature payload; `tests/test_town_features.py` |
| DZ-14 | Энергия, хлеб и отдых / LIVE | engine `eat`, `advance`; features cook/rest | Energy snapshot, у еды реальный расход склада; нет награды за отдых |
| DZ-15 | Навыки каждой роли, характеристики и вещи / LIVE | features `skill`, `parameters`, `action`, `view` | `town_skills`, `town_profiles`; старые бонусы имеют лимит, новый рост не согласован |
| DZ-16 | Земля, общий/внешний запас / LIVE | `sramoslav/town_realm.py`: `bounds`, `inside`, `work_scope`, `reserve` | `town_walkers`, свойства участка; scope world/player; клиенту нельзя доверять funding |
| DZ-17 | Дома, имущество, социальное жильё / LIVE | realm `PROPERTY`, `HOUSE`, `action`, `place`, `refund` | `town_properties`, `town_housing`; pending/approved/ready; `tests/test_town_realm.py` |
| DZ-18 | Помощники и заявки / LIVE | `sramoslav/town_management.py`: `appoint`, `action`; `sramoslav/town.py` команды | `town_assistants`, `town_proposals`, log; `tests/test_town_management.py` |
| DZ-19 | Рынок, покупка у казны, собственность / LIVE | realm `action` stock/market/trade/property; features credit | `town_market`, stock, treasury; товар передаётся, а не возникает |
| DZ-20 | Старая тестовая оплата / LIVE | features `WAGES`, `credit`, `record` на 0.4.2 | 1/2/5 тестовых свечей, не основной счёт батюшки |
| DZ-21 | Дезкоины и миграция номинала / BRANCH | features `WAGES`; `sramoslav/town_accounting.py`: init/income | Старые остатки/цены ×10 один раз, 10/20/50 ДЗК; ledger/income |
| DZ-22 | Обмен и отдельный кошелёк / BRANCH | accounting `exchange`, `wallet`, `day` | `town_exchange`, `town_candle_wallet`; `tests/test_town_accounting.py` |
| DZ-23 | Изготовление, журнал материалов, себестоимость / BRANCH; общий вклад / LIVE | features record/view, accounting materials/take/put/report | `town_contributions`, `town_material_log`, `town_basis`, `town_income`, asset basis |
| DZ-24 | Налоговый пример и ИИ-бухгалтер / BRANCH | accounting `tax`, `report`; `sramoslav/town_accountant.py` | Налог не списывается, отчёт owner-only, advice без мутаций; тесты accounting |
| DZ-25 | Чат, стикеры, антифлуд и батюшка / LIVE | features action/view; `sramoslav/town_chat.py`: tick/claim/answer | `town_chat`; `tests/test_town_chat.py`; модель не решает права/деньги |
| DZ-26 | Видимость и перемещение / LIVE | engine snapshot, realm move/path/tick; старый VillageWorld.ts | online ИЛИ работает; offline idle скрыт; `tests/town_realm_browser.py` |
| DZ-27 | Ошибки, выбор действия, Android / LIVE fixes; улучшение текста / BRANCH | `assets/town/game.js`, `realm.js`, gateway ошибки; `tests/town_043_browser.py` | Выбор не теряется при обновлении, причина доступна без hover |
| DZ-28 | Уведомления / LIVE code; устройства не проверены полностью | `sramoslav/town_push.py`, gateway pending/claim; notifications JS | подписки/opt-in/notify; `tests/test_town_push.py`, `tests/town_notifications_browser.py` |
| DZ-29 | Мирные события, разведка, достижения, эмоции / LIVE основа | features `tick`, `action`, POINTS/EMOTES; realm pranks | `town_incidents`, exploration/contributions/emotes/pranks; без боевого урона |
| DZ-30 | Замок, новые границы и ночная стройка / PLAN | прежний TODO; DECISIONS.md | Цены/размеры/ночной коэффициент не определены |
| DZ-31 | Больница/тюрьма и дуэльный срок / LATER | прежний TODO и ограничения других игр | Только план; не копировать весь кошелёк/модерацию/базы бота |
| DZ-32 | Регионы, уровни материалов/станков / LATER | feedback #47, VIEWER_BACKLOG | Семь ступеней — пожелание, не принятый баланс |
| DZ-33 | Текстовые данжи, экипировка, медицина / LATER | #44/#45 и дополнение владельца | Сохранение добычи, текстовый бой; числа/потери не утверждены |
| DZ-34 | Кланы, атаки, новые ускорения, очередь от ума / EXCLUDED | последние решения владельца | Не включать из каталога основы или старых зрительских советов |
| DZ-35 | Новая графическая переделка / PAUSED | отмена пунктов 1–4; сохранённый visual plan | Использовать существующий React/PixiJS UI автора, не переносить новый Canvas shell |

## Числовой контракт старой версии

База обычных работ (без уже существующих ограниченных навыковых модификаторов):

| Задание | Время, с | Энергия | Выход |
| --- | ---: | ---: | --- |
| wood | 90 | 14 | 2 дерева |
| stone | 120 | 18 | 2 камня |
| build | 180 | 22 | 1 строительная работа |
| grain | 90 | 12 | 4 зерна |
| bake | 120 | 14 | по рецепту |
| walk (переход между берегами) | 60, дороги влияют | 2 | нет награды |
| rest | 60 | 0 | восстановление по серверному правилу, не деньги |

Профессия 7200 реальных секунд; игровой день 3600 секунд. Присутствие/crew в
старом коде учитывает seen за последние 20 секунд; число жителей не равно онлайн.
Условия ролей, берега, зданий и напарника проверяются на сервере при старте,
а очередь повторно проверяется при запуске.

Лес: capacity 216 и 18 ресурсов/час на участок, полный пустой запас за 12ч.
Камень: 162 и 13.5/час, полный за 12ч. Руда: 36 и 9/час, полный за 4ч.
Регенерация дробная/постепенная по серверным timestamps; не сбрасывается в начале дня.

Мост: 60 работ; деревянный забор: 12; стены: 20. На объект обычно 3 места,
расширения/улучшения меняют доступность; нельзя обещать больше работ, чем осталось.
Мост и забор могут строиться одновременно. Второй берег: склад → ферма →
пекарня → дороги; другие здания имеют собственные зависимости.

Общие уровни: 1–3; подготовка target×3 работ, расширение до 2 уровня — 20,
до 3 — 40. Износ производства −0.5, дорог −0.1 при соответствующей работе,
нижняя граница состояния 30; ремонт +8, максимум 100. Это не боевое разрушение.
Код start развития — источник фактической энергии; UI-число не заменяет его.

## Рецепты: именно код, а не предположение

| kind | Время, с | Роль | Вход → выход |
| --- | ---: | --- | --- |
| boards | 90 | wood | 4 wood → 2 boards |
| blocks | 100 | stone | 3 stone → 2 blocks |
| flour | 90 | mill | 6 grain → 3 flour |
| bake | 120 | bake | 6 grain → 6 food; НЕ flour в старом коде |
| iron | 150 | iron | участок → 2 iron |
| metal | 120 | craft | 3 iron + 2 wood → 2 metal |
| tool | 120 | craft | 3 boards + 2 blocks → 1 tools |
| cart | 150 | craft | 6 boards + 2 metal → 1 carts |
| fish | 90 | помощь | → 1 fish |
| garden | 90 | помощь | → 2 vegetables |
| cook | 90 | помощь | 1 fish + 2 vegetables → 2 meals |
| repair | 75 | помощь | 1 boards → ремонт |
| scout | 180 | помощь | 1 meals → 2 boards; ограниченные открытия |

Полный контракт включает здание/берег/запас, роль, energy и доступность, а не
только таблицу. В C# не переписывать строки один к одному без учёта ResourceKind.

## Владение и экономика

- Внутри одобренного царства используется общий запас. Внешние постройки/работы
  используют свой внешний запас, не общий. Купленный общий товар становится личным.
- Дома в 0.4.2: 6/12/20 работ; материалы 12 boards + 6 blocks,
  30 + 18, затем 70 + 40 + 8 metal. Стоимость 30/90/240 тестовых свечей;
  в подготовленном новом номинале 300/900/2400 ДЗК. Не умножать повторно.
- Рынок передаёт существующий товар, сохраняет владельца/количество/остаток;
  кошельки не отрицательны. Зарплата за работу, не за каждую единицу груза.
- ДЗК и тестовые свечи — разные счета. Обмен 100→1, максимум 5 за реальные
  сутки в настроенной timezone, replay key уникален на игрока. В текущем
  тестовом режиме основной лимит 20 игр и кошелёк батюшки не изменяются.
- Налоговый пример 200/500/1000 и 0/10/20/30% не утверждён для списания.
  Торговля: известная прибыль, не оборот. Неизвестную историческую себестоимость
  помечать неизвестной; возвраты/миграция/перевод не новые заработки.
- ИИ не изменяет кошельки, роли, цены, права или таймеры. Батюшка: пауза 90с,
  максимум 12 реплик/час, инициативные примерно раз в 8 минут при активности.

## Найденные различия и риски проверки

1. В C# `TaskDefinition.WorkSeconds` уже есть — не требуется новый движок для
   разных сроков. Но `CycleEngine` добавляет дорогу и `SkillRules` меняет время.
2. C# `ResourceKind.Gold` — ресурс, не готовый личный кошелёк дезкоинов.
   `Food` не равнозначно всем bread/fish/meals; отсутствующие виды расширять явно.
3. C# `NextAction.tsx` — индикатор/отмена текущей работы, НЕ очередь следующего.
4. `CitizenStatus.Active/Dormant` не заменяет online ИЛИ working; нужен heartbeat
   и separate presentation predicate, работа офлайн должна продолжаться.
5. `VillageHub.JoinVillage` в просмотренном коде добавляет подписчика в группу
   после общей авторизации, без собственной проверки членства/игрового ограничения.
   Перед интеграцией проверять доступ на подписку и повторно на команды.
6. `GameService` имеет VillageLocks и retries, а EF entities — rowversion.
   Это полезная основа, но новые денежные операции и job receipts должны быть
   защищены транзакцией/уникальными ключами, а не только lock в одном процессе.
7. Быстрый визуальный подход не означает ускоренную серверную добычу.
   Сохраняем работу на месте и груз, но не переносим новый рендер старой ветки.
