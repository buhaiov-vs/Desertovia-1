# D-100: редактор набегов с ползунками установлен и независимо проверен

Версия остаётся0.7.11; это UIhotfix D-099 по команде владельца «Грузи».
Проверено2026-10-17T14:21:38.6374261Z. Прежние NOTREADY/NOTINSTALLED ниже — история.
Game exact55094d52755eb074e8074e41374a9ec5fd3a96ed; bot exact092390d83cf9c1e6a110633f5fb02b340742688f.
Exact gameCI39038955924 иbotCI39039077720 attempt1 SUCCESS; game516Domain/
270API/199browser+1штатныйskip, frontend/lint/DockerPASS;
bot903tests/serverSKIPPED;31operator guardsPASS. Mergeдеревья равны testedsource.
Actualpreflight39040135022, install39040182761 и отдельный
independentcheck39040394389 SUCCESS. Новыйraidui0711flow; старые
post0711andolderrequests не повторялись. Currentraidui0711requestactioncheck.

120→120профилей,0потерь навыков/характеристик/славы;
122аккаунтов/122привязок сохранены.
34миграции без изменения схемы. Глобальные номера/семейныйреестр безошибок.
Publicfrontend0.7.11/24JSchunks, новыеsliderJSmarkers
и liveSHAlookupбатюшки подтверждены. FreshPGdump restorecatalog+SQLite/code/
config/profile/identity/combat backups сохранены приватно. Инвентари/профили не
сбрасывались; обычная игра продолжалась. Толькоfeedback_context.py runtimeбота
заменён: mappingновогоSHA;16othermodules/AIbudget/quarantine/config/Propose
сохранены. Серверныйsrc tree равен прежнему; баланс/формулы/DBschema прежние.

Установлены: Босс/Смешанный/Орда, быстрый выбор сложности, свёрнутые advanced;
все числовые поля и доли видов — nativeползунки с единицами/пояснениями ×;
общая сумма100%, Поровну, точные ошибки до preview/save/launch; прежниеmanual
presets/idempotency/ownerlimits сохранены. Все5деревенскихвидов доступнывладельцу,
DungeonOnlyвраги исключены. Новые боевые пробы/модели/сообщения/платежи не запускали.
Не делать performanceзаявлений без естественного боя/замеров телефона.

Не переустанавливать и не подставлятьmain/mergeSHA вместо exacttestedsource.
Новый выпуск требует отдельного свежегоcandidate/preflight/backups/check.
Нельзя автоматически откатывать двигатель через activeRaids/newJSON.

# 0.7.11 установлена и независимо проверена — 17 октября 2026

D-098: владелец явно поручил «Установи». Проверка завершена в13:25:32UTC
(15:25:32Europe/Oslo). Ранее записанные NOTINSTALLED/дата не выбрана — история.

- Game exact `ba7050f8d0d2efc07d7523d300944e9531c3fb01`, CI39031620418 attempt1
  SUCCESS:516Domain,270API,193browser PASS/1штатныйphonekeyboard skip,
  lint/TypeScript/frontend/Docker PASS. PublicPR54 mergedcc917669, дерево совпало.
- Bot exact `2d5a7ab7759bd7d50322335bbe1772267396ae5d`, CI39031678709 attempt1
  SUCCESS903tests/serverSKIPPED. CanonicalPR52 merged645bc0f, дерево совпало.
-29защитных тестов оператора PASS. Свежий preflight39036214937,
  установка39036254787 и отдельная независимая проверка39036486712 SUCCESS.
-120→120профилей,0потерь прогресса;122аккаунта и122привязки входа сохранены.
  Проверены навыки/характеристики/слава. Старые глобальные номера и счётчик
  корректны; новая нумерация по семействам без пропусков/дублей/пустых значений.
-34миграции применены. Публичный frontend0.7.11/24JSchunks и определение версии
  батюшкой по фактическому liveSHA подтверждены; feedback_origins создана.
- Свежие PostgreSQLdump с проверкой restorecatalog, SQLite/code/config/profile/
  identity/combat backups сохранены приватно. Данные/инвентари не сбрасывались;
  обычные траты/работа во время живой игры не означают неизменность инвентаря.
- Runtimeбота заменены только app/db/helper/feedback/civic_notifications/
  feedback_context.py. app сохраняет reviewedbudgetfix, добавлены4строки захвата
  предложений;11остальных защищённых модулей, настройки, AIbudget/quarantine и
  Propose сохранены. Дозоров/привязки к костру/штрафа потерь рюкзака не добавляли.
- Принудительные волны, модельные пробы, сообщения и платежи не запускались.
  Во время обновления не было активного боя/естественного замера: плавность
  телефона в настоящем набеге требует наблюдения игроков, не объявлена доказанной.

Установлены: физическое спасение и ускорение лечения после доставки; редактор
БОСС/смешанный/орда с настройками и preview; адаптация и фактические итоги боя;
номера зданий по типу; расход еды/вместимость; оплачиваемое улучшение укреплений
дерево→брёвна→камень с пиксельной обводкой и дневной/ночной графикой; боевой XP,
вложение очков исследований и два блюда; сохранение явных предложений батюшке.

Не переустанавливать и не повторять старые requests. Текущий post0711request
имеет actioncheck. Новый runtime уже использует новые JSONполя; автоматический
откат старого двигателя запрещён, особенно при незавершённых RaidBattles.
Полная фактическая квитанция хранится в приватном ops release record.

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
