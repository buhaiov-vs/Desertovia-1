> **D-048 · подробный следующий заход:** [план исправления регрессий](REGRESSION_FIX_PLAN_0_5_2.md).
> Конкретные файлы, переходы чата, геометрия подписей, серверные резервы,
> совместимость оплаченных циклов, энергия и итоговая проверка. Сейчас PLAN.

# Продолжение пакета0.5.2 после сжатия

15.10.2026. Последнее поручение — выполнить ВЕСЬ пакет, затем проверка.
Не возвращаться к старому «только план». Сервер НЕ обновлять: код и GitHub.
Последнее уточнение: не ряд коек; разнесённые ники/причины/таймеры пациентов.
Читать CURRENT_MASTER_PLAN.md, BALANCE_PROPOSAL.md, IMPLEMENTATION_LOG.md.

## Рабочие ветки

- Игра: `/workspace/Desertovia-batch`, feat/cooperative-update, canonical main
  desertod666/Desertovia. Первый commit5bc913b, последующие исправления в этой
  же ветке. Кандидат0.5.2; live исторически0.5.1/ad88be3, не проверялся заново.
- Бот: `/workspace/TG-bots-batch`, feat/cooperative-update. Canonical
  claude/telegram-bot-chat-variety-wci5ph, НЕ main. Commitc168c01, PR34:
  https://github.com/desertod666/TG-bots/pull/34. CI test PASS, server SKIPPED.
  Сохранены tone8239cf9 и proxy guard; старую hospital ветку не деплоить.
- Git author Kyrylo Tieriaiev <tieriaievk@gmail.com>, коммиты English.
  Нет разрешения merge/deploy/reset/реальных оплат/Telegram-рассылок.

## Реализовано

Все18 блоков общего плана: готовая больница/52 обводки/2x undead/кнопки,
серверный запрет берега, версия нового6-этапного моста, полезные XP/слава,
износ/ремонт, склады30→60, заявки/снос, жильё12/20/28, read-only браки по TG ID,
усталость/обморок, напиток, редкий контекстный чат, мобильный UI и ночная тема.
Дополнительно переноска раненого: RescueRules/GameService.Rescue,5мин санитары,
полевой лазарет без здания. Срок/вещи не меняет. Боевое снадобье отдельно от
платных дуэльных тарифов. Карточки всех пациентов с полным ником; на карте
до4 разнесённых коек, свой всегда среди них.

Миграции additive: CooperativeVillageRules (JSON backfill/старый мост),
WoundedTransport, SupervisionWorkSnapshot. Legacy overflow разрешено забрать,
не обрезать; неизвестный старый investment не превращать в придуманный refund.
Внешняя больница по умолчанию выключена Game__HospitalEnabled=false.
Обычный native rescue работает независимо от внешнего bridge.

## Проверки и продолжение

Фактические окончательные результаты и ссылки записывать в IMPLEMENTATION_LOG.md,
не в историю старых этапов. Локально уже проходили321 Domain, большинство176 API
(две старые проверки исправлены и отдельно прошли),28 bot и телефон/ПК; позже
добавлены4 domain случая и проверка отсутствия пересечения подписей пациентов.
Финальные целевые прогоны и CI относятся к итоговой ветке. Полный набор после
каждой мелочи не повторять; обязательный финальный CI оставить.

Инструменты: dotnet `/workspace/dotnet/dotnet`, EF `/workspace/desertovia-tools/dotnet-ef`,
DOTNET_ROOT=/workspace/dotnet и PATH с этим каталогом. Node modules web — symlink
на основной worktree, не добавлять в git. Bot python `/workspace/TG-bots/.venv/bin/python`.
Не строить Vite в wwwroot одновременно с .NET build: static assets race.
WebGL software медленный, не грузить одновременно браузер и backend suite.

Браузерный локальный Postgres cooperative-postgres, localhost55439,
DB desertod_e2e, стандартные локальные dev credentials из playwright.config.ts.
DESERTOD_E2E_CONNECTION передать с Port=55439. Это НЕ live DB.
API тесты создают собственные Testcontainers. `gh pr checks --json` в этой
версии gh не поддерживается; читать `gh pr view --json statusCheckRollup`.

Публиковать одну ветку/PR на repo. Game CI сам проверяет backend/frontend/e2e/
Docker без deployment. Bot PR CI test, server job должен оставаться skipped.
После итоговых проверок обновить progress.json, документы/патчноут, записать
ссылки на оба PR. Ничего не merge/deploy без следующего поручения владельца.
