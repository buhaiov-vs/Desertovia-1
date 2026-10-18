# Отчёты прямо из игры

Статус: установка обеих частей 0.7.15 разрешена владельцем. Финальные проверки и установка ещё не завершены; live0.7.14.

## Решение владельца

Маленькая кнопка «!» без текста, выше карты и любого меню. Три типа: ошибка,
жалоба, предложение. Автоснимок текущего вида с меню + версия/персонаж/диагностика.
Обязательны «Отмена» и крестик. До «Отправить» никакой записи в БД/Telegram,
localStorage/IndexedDB/файл; память черновика очищается при отмене/закрытии.

## Точки входа и выходы

1. App монтирует ReportButton через portal в body выше модалок, карта и Dungeon
   регистрируют capture через Pixi renderer.extract, контекст — useReportContext.
2. Нажатие извлекает WebGL кадр, html2canvas добавляет DOM меню. Снимок до формы;
   JPEG ограничен350KB, длинная сторона1280px. Экран игры, не ОС/браузер.
3. Форма: тип, описание, предпросмотр/удаление снимка, отмена, отправка. Ошибка
   снимка не блокирует отчёт. Закрытие не вызывает сетевую запись.
4. POST /api/reports: cookie auth, account/character из БД, server SHA из config,
   client fields отдельно; только allowlist, без cookies/паролей/сырых логов/чатов.
   Account advisory transaction lock,5/h, idempotency UUID+fingerprint, новая
   независимая таблица PlayerReports. Игровые locks/ticks не вызываются.
5. Worker вне боя передаёт через прежний authenticated loopback bridge
   director kind=feedback_notify. Pending сохраняется при недоступности бота.
6. Bot отдельный lock, атомарно сохраняет предложку+identity/context+receipt;
   только owner из configured owners. Один ZIP: report.json/report.txt/screen.jpg,
   caption с автором/версией/кратким текстом. Пустой TG ID не становится человеком.
   Подтверждённые отказы Telegram можно повторять, Unknown не дублировать вслепую.
7. Доставленные картинки удаляются из PG через7дней, недоставленные сохраняются.
   Бот не дублирует картинки в SQLite. Общий жёсткий предел вложений256MiB: при заполнении текст/данные сохраняются,
   форма явно сообщает, что снимок не приложен.

## Проверка перед review

- API: auth/access, author spoof ignored, idempotency/conflict, quota, invalid and
  oversized JPEG, no-game-mutation snapshot, durable retry/delivery/retention.
- Bot: owner routing, atomic save, repeat/restart, known/unknown send failures,
  ZIP content, no-Telegram identity, old director131KB limit still applies.
- Browser: «!» above menus; Cancel/X/Escape send nothing and clear draft,
  reopening fresh, no browser storage, JPEG includes original Pixi and DOM/menu,
  absent report form, remove image, errors/retry, desktop/Android/iOS emulation.
- Inspect actual game screenshots. Full relevant suites/lint/build and CI.
- No actual/synthetic production messages, model probes, waves or player writes.

## Ограничения

HTML screenshot compatibility must be checked across browser engines. Failure
falls back to structured report. Screenshot can contain visible game chat: player
sees preview and may remove it. User typed text is sent only after explicit submit.
A submit already acknowledged by server is a real report; closing receipt does
not retract it. No code here is deployed by writing this source.

## Safari follow-up

Визуальная проверка после первого зелёного CI нашла перезапуск CSS-анимации
меню в клоне WebKit: просвечивали слои HUD. Отключаем animation/transition
только в клоне, не в живой игре. Новый browser assertion сравнивает настоящий
пиксель JPEG с непрозрачным фоном меню. Ожидаем финальные проверки нового SHA.
