# Как продолжить после сжатия контекста

Дата15.10.2026. Сначала читать CURRENT_MASTER_PLAN.md, затем BALANCE_PROPOSAL.md
и BATCH_UPDATE_PLAN.md. Это сохраняемый контекст, не новое поручение на запуск.
Последний запрос владельца — обновить/сохранить план. Код большого пакета НЕ начат.
Не трактовать завершённый документ как разрешение на реализацию/выпуск.

## Рабочие репозитории

- Публичная игра: desertod666/Desertovia, canonical main. Документы собраны в
  ветке docs/batch-update-plan, рабочее дерево `/workspace/Desertovia-battle-plan`.
  Путь исторический: всегда проверять git branch/status, не судить по имени каталога.
- Бот: desertod666/TG-bots; canonical **claude/telegram-bot-chat-variety-wci5ph**,
  не main. Выложенные правила беседы source8239cf9, PR33. Рабочее дерево
  `/workspace/TG-bots-chat-tone`; branch fix/contextual-chat-tone уже merged.
- Операции Oracle — только существующие private Actions desertod666/bolig-agent.
  Не использовать housing main для bot/game deployment. Private worktree
  `/workspace/bolig-player-progress-audit`, branch ops/player-progress-audit.
  В нём private audit/recovery/release receipts; не копировать в публичную игру.
- Прямого SSH/VPN здесь не было. Окружение/доступ проверить актуально, не угадывать
  адреса и не печатать env/токены. При managed cloud читать runtime skill и readiness.

Git author: Kyrylo Tieriaiev <tieriaievk@gmail.com>, коммиты на английском.
Не force-push чужие ветки; не трогать чужую незакоммиченную работу.

## Готовые исходники для выборочной интеграции

| Работа | Ветка/путь | Проверенный источник/состояние |
| --- | --- | --- |
| Hospital game |feat/hospital, `/workspace/Desertovia-hospital`|PR23,45cfccd;311 Domain+171 API и59 browser; ещё не live |
| Hospital bot |feat/desertovia-hospital, `/workspace/TG-bots-hospital`|PR32,9da022d;782 Python tests; основан до bot33 — интеграция, не overwrite |
| Готовые обводки |tools/sprite-outline, `/workspace/Desertovia-outline-tool`|PR18,507e92b;52 готовых PNG, не live |
| Убрать leave/delete |fix/hide-destructive-city-actions, `/workspace/Desertovia-hide-departure`|PR24; включает также docs отзывов48–52; выбрать нужный diff, не потерять историю |
| Новый стиль бот-чата |fix/contextual-chat-tone, `/workspace/TG-bots-chat-tone`|PR33 merged8239cf9;778 tests+CI38685463373; bot-only patch уже установлен |

Планы отдельными PR:25 командир,26 осложнения,27 чат,28 мост,29 жильё/снос/браки.
Их актуальные отдельные документы теперь скопированы в docs/batch-update-plan.
Это копирование документации, а не merge реализаций/ассетов. HOSPITAL_PLAN.md и
старые TODO могут описывать прежний этап; CURRENT_MASTER_PLAN имеет приоритет.

## Последние реальные проверки и операции — не повторять как задачи пакета

- Game live0.5.1/ad88be3. В последний раз15.10 после bot-only выпуска проверен
  серверный публичный providers endpoint200, Telegram/browser auth включены,
  development auth выключен. Bot/housing service active. Перепроверить при старте
  нового deployment; старый health не гарантирует будущую готовность.
- Bot-only chat tone: report38687429902, install38687482714,
  post-check38687570637. Только brain.py/town_proxy.py/conversation_tone.py,
  конфиг/ключи/балансы/.NET release не изменялись. Есть private backup/receipt.
- Executor public HTTP probe однажды вернул403, server public probe200. Не
  объявлять это падением игры и не обходить сетевые запреты; разбирать слой ошибки.
- Восстановление одного профиля и массовый разбан уже выполнены; секретные
  детали только в private operator repo. Не повторять и не смешивать с лечением.
- AI audit обнаружил2 auth-fail credentials из33,31 отвечал; модель3.5 Flash
  возвращала503/429, Flash Lite отвечала. Ключи/порядок моделей НЕ менялись.
  Новый tone release не означает смену модели или удаление плохих ключей.

## Что делать при следующем «выполняй пакет»

1. Открыть общую таблицу18 блоков и числовой baseline. Сверить refs/status,
   подготовить интеграционные ветки. Действовать по последнему разрешённому объёму.
2. Не возвращаться к рисованию интерфейса/спрайтов, не писать новый движок,
   не отключать функции автора по старым архивным ограничениям.
3. Делать все правки, писать нужные новые тесты, но общие тестовые запуски только
   после окончания кода. Не разбивать работу на18 остановок ради новых подтверждений.
4. После кода пройти финальную матрицу BATCH_UPDATE_PLAN. Любое обнаруженное
   ограничение описать честно, не маркировать незавершённый блок как done.
5. Не выкладывать без разрешения; не ждать его для обратимого кода/чтения/проверок.

Если окружение заменено и локальных worktrees нет, восстановить их из указанных
репозиториев/веток и PR, затем прочитать сохранённые документы. Не полагаться на
старый memory store инструментов, логи PTY или эту переписку как единственный архив.


## Последнее дополнение D-040

Владелец отдельно запросил увеличение маленьких зомби/скелетов. Код подготовлен
в fix/larger-undead,07d8f66 (только enemies.ts + справка). Масштаб2 запечён в
GraphicsContext, enemyHeight обновлён; поворот scale.x не сбрасывает размер.
Карта и данж используют общий drawLook. Не LIVE и не интегрировано в пакет;
проверки отложены до общей финальной фазы по просьбе владельца. Выборочно перенести
эту правку в блок18, не рисовать заново и не менять боевые характеристики.
