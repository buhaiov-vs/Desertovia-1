# Дезертовия 0.7.11 — пакет в разработке, НЕ установлен

Владелец D-097 разрешил реализацию сохранённого плана. Ветка feat/patch-0-7-11.
Дата установки0.7.11 не назначена. READY/installed не объявлять по наличию кода.
LIVE последний независимо проверенный0.7.9/7c738bc; прежние ops requests не трогать.

Состав: социальное спасение/доставка в больницу, редактор набегов и адаптация,
ограничения потерь/живой носитель кражи, номера внутри типа здания, расход еды,
вместимость жилья/склада, прогрессия укреплений с совпадающими day/night tiles,
боевой statXP, вложение ResearchPoints в выбранное исследование, два блюда,
настоящее сохранение явных ownerпредложений батюшке и понятные civicnotifications.
Подробная реализация и остающиеся проверки: PATCH_0_7_11_IMPLEMENTATION.md,
NEXT_PATCH_PLAN.md, RESCUE_AND_RAID_CONTROLS_0_7_11.md,
NUMBERING_FOOD_SPACE_0_7_11.md, FORTIFICATIONS_0_7_11_IMPLEMENTATION.md,
PROGRESSION_AND_MEALS_0_7_11.md. Публичные patch notes добавлены0.7.11, версия
изменена только в feature branch; 0.7.12…0.7.99 остаётся выбранной политикой.

## Сохранение и миграции

Существующие профили/вещи/характеристики/навыки/входы/стройки/ранения не сбрасывать.
Миграции28–34 additive: SocialRescue0711, RaidEditor0711,
RaidLossAccounting0711, RaidParticipation0711, BuildingDisplayNumbers0711,
FortificationLevels0711, ProgressionAndMeals0711. RaidAdaptation defaultCoefficient1,
новый displayregistryбезперезаписи Number/Sequence, старые стеныLevel1/Damage
не меняется; Research KnowledgeWork0, Meal поляnullable/пусты. Старый JSON боёв
принимает nullableполе RangedDamage. Нет потерь личного рюкзака при смерти.
Нельзя делать автоматический downgrade при новых JSON/незавершённых RaidBattles.

## Bot payload / будущая установка

SourcebranchTG-bots feat/patch-0-7-11 на canonical
claude/telegram-bot-chat-variety-wci5ph. В этом патче изменены app.py, db.py,
helper.py, feedback.py, civic_notifications.py; feedback_context.py получает
отображение финального gameSHA→0.7.11 (шестой файл, только при совпадении liveheader). Futurepayload НЕ ограничивается
старым feedback_context.py или прежними двумя civic файлами: новый оператор
должен проверить actual before/after protected hashes и сохранить установленный
AI budget/quarantine/llm/town_proxy и режим директора Propose. Нельзя заменить
старой app.py сервер с чужими новыми изменениями. Не менять private main/botmain.

Новый reviewable operator/candidate делается только после точного успешногоCI;
до этого manifestready=false/отсутствует, никакого deployrequest. Фактический
preflight, PostgreSQL restorecatalog, SQLite/config/code/profile backups и
независимый check обязательны для будущей разрешённой установки. Старые
проверки0.7.9 не заменяют эти действия. Никаких live syntheticwaves/messages/
modelprobes/payments/resets в ходе проверки. PR draft и журнал отражают текущий
неполный статус. CI pins/receipts вписать только после фактического результата.

## Проверка перед окончательным CI

516Domain + final17 PASS, API270 local/exactpreliminaryCI PASS, новые8WALL+4P2browser
PASS на телефоне/ПК, frontend0.7.11/TS/lint/DockerPASS. Предыдущие отдельные пакеты
имеют свои focused receipts в PATCH journal. GamePR54/botPR52 остаются draft,
пока все обязательные jobs на финальном source не завершатся SUCCESS. Ссылки на
финальные CI и сохранённые фактические receipts находятся в checks этих PR и
privateops deploy/PATCH_0_7_11_PREPARATION.md; не подменять ими livepreflight.
