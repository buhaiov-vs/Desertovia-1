#0.7.9 — набеги на имущество, орда и боссы

D-092: владелец17октября разрешил установку сейчас и наблюдение игроками.
Статус: подготовка, НЕ установлен. LIVE0.7.8/aab588f остаётся до фактической
установки и независимого check.

Подробный состав, источники и контракты: [MONSTER_BEHAVIOR_INTEGRATION.md](MONSTER_BEHAVIOR_INTEGRATION.md).
Новый runtime номер0.7.9, без скачка до0.8/1.0. Рюкзаки при поражении не списываются.

Предыдущий CI39016736130 на7e50c66 провалил1из255 API: CombatLockContentionTests
захватил параллельный плановый медицинский опрос другой деревни.460Domain PASS.
Проверка изолирована по всем actor IDs тестовой деревни, включая царя.
По-прежнему обязательны ровно2 свежих actor-only опроса, отказ при новом ранении/
недоступном bridge и отсутствие лечения других жителей. Runtime не ослаблен.

До READY требуется новый полный точный game CI: Domain/API/browser/lint/build/
Docker. В бот меняется только feedback_context.py: регистрация точного gameSHA
как0.7.9; все15 остальных установленных модулей, бюджет, карантин и Propose
сохраняются. Новый точный bot CI обязателен.

Новый приватный post079-candidate/operator/workflow. Сначала report на фактическом
baseline aab588f либо уже exact candidate; при чужих изменениях остановиться.
После PASS — deploy с actualPGdump/restorecatalog,SQLite/config/code/profiles/
identities backups; затем отдельный requestcheck. Не replaypost078. Проверить
27миграций сMonsterBehavior, старый JSON, точныйSHA, frontend0.7.9, Telegram
routing, навыки/характеристики/вещи/привязки/номера зданий и сохранность режимов.
Нет реальных сообщений, синтетических волн, модельных проб, платежей или сброса.
Не откатывать JSON writer/двигатель автоматически при активныхRaidBattles.

Точные pins,CI receipts,install/checkrunIDs и фактическую сохранность записать
сюда после проверок. READY не равно installed.
