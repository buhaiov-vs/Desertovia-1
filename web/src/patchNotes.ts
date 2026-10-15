import type { Language } from './i18n/translator'

/** Only documented, released changes belong here. Plans are kept in docs/dezertovia. */
export interface PatchNote {
  version: string
  date: string
  title: Record<Language, string>
  changes: Record<Language, readonly string[]>
}

export const PATCH_NOTES: readonly PatchNote[] = [
  {
    version: '0.6.1', date: '2026-10-15',
    title: { ru: 'Удобнее на телефоне, восстановление сил и ответы ИИ', en: 'Mobile menus, energy and AI replies' },
    changes: {
      ru: ['Посадник может предложить любое доступное здание, включая больницу и замок. Стройка начинается после одобрения царём.',
        'Исправлено завершение хижин и сохранение вклада: ошибка одного работника больше не откатывает труд других; отчёты отправляются после сохранения.',
        'Вкладки меню в одну строку, нижняя панель с едиными иконками. Кнопка возврата открывает карту без перехода на стартовый экран.',
        'Дома и заявки разделены на сворачиваемые списки. В заявках указаны здания; невозможные улучшения не создают новые заявки.',
        'Ники игроков можно скрыть в настройках. У пациентов на карте остались статус и причина без длинного таймера.',
        'События работы по умолчанию скрыты в чате, одинаковые соседние отчёты объединяются со счётчиком.',
        'Всевышний предлагает настройку силы и перерыва взвара; изменения требуют согласия царя.',
        'Энергетик батюшки восстанавливает все силы за 300 ДЗК или 3 тестовые свечи раз в 12 реальных часов. Не снимает ранения и обморок.',
        'Исправлены таймаут и обрезка ответов ИИ; журнал различает причины сбоев.'],
      en: ['Governors can request any available building, including hospitals and the castle. Construction starts after Tsar approval.',
        'Fixed hut completion and contribution saves: one failed worker no longer rolls back others; reports are published after commit.',
        'Single-row menu tabs and an icon dock; return to map without leaving the game.',
        'Separate collapsible homes and applications. Unsupported upgrades no longer create applications.',
        'Player name visibility setting. Short patient status without a timer on the map.',
        'Work events hidden by default; repeated adjacent reports grouped with counts.',
        'Owner-approved director tuning for vigor drinks.',
        'Full energy for 300 DZK or 3 test candles, once per 12 real hours; no healing or waking.',
        'Aligned AI timeouts, preserved structured replies and specific failure reasons.'],
    },
  },
  {
    version: '0.6', date: '2026-10-15',
    title: { ru: 'Царство, дезкоины и Всевышний', en: 'The kingdom, dezcoins and the Almighty' },
    changes: {
      ru: ['Полное восстановление сил при хороших условиях теперь занимает12 реальных часов. Перед третьей работой с нехваткой энергии показано предупреждение об обмороке; его срок сохранён. Таймер показывает оставшееся реальное время.',
        'Добавлены царский замок и его расширения, личные стройки за границей царства за собственные материалы.',
        'Тестовые дезкоины оплачивают полезную работу. Личное имущество можно передавать и продавать; общий материал сначала покупается у царства. Обмен — только на тестовые свечи с личным лимитом и фондом царства. Налог пока показывает расчёт без списания.',
        'Журнал показывает изменения запасов и назначения расходов. Добавлены упоминания жителей, выключение игровых всплывашек и компактная энергия/сытость.',
        'Основной город и прогресс персонажа защищены от удаления прямым запросом.',
        'Единый ИИ-директор наблюдает за городом, помнит замысел и предлагает события, передышки и нужную работу. Применение — после согласия царя.',
        'Батюшка присылает предложения царю в личку с кнопками согласия и отказа. В «Городе» появились панель Всевышнего, журнал и переключение режима.',
        'Анализ ИИ раз в15 минут, важные изменения — от5 минут; не больше4 анализов в час. Новое предложение — не чаще раза в30 минут.',
        'Направленные подходы скелетов используют серверный маршрут. Охрана выбирает участок; перестроение занимает30 реальных секунд. Против направленной атаки резерв Авто даёт75% личного вклада.',
        'Экономические советы опираются на запасы и наблюдения. Свечи, цены и налоги автоматически не меняются. Возможен добровольный сбор в существующий данж.',
        'Версия в настройках открывает историю обновлений. Враги ждут конца таймера; сроки лечения показывают реальные дни, часы и минуты.'],
      en: ['Full energy recovery in good conditions now takes12 real hours. A third tired-work action warns about fainting; its duration is unchanged. Recovery shows explicit real remaining time.',
        'Added a royal castle with territory upgrades and privately funded construction outside the kingdom.',
        'Test dezcoins reward useful work. Personal goods can be transferred and traded; common stock must first be purchased. Exchange grants only test candles with personal and kingdom limits. Tax remains a preview without deductions.',
        'A journal records stock changes and spending. Added resident mentions, an in-game popup preference and compact energy/hunger.',
        'The primary kingdom and resident progress are protected from destructive direct requests.',
        'One AI director watches the village, remembers its intent and proposes events, recovery windows and useful work. Effects require royal approval.',
        'The chaplain privately sends the owner approval/rejection buttons. Village now has a director panel, journal and mode controls.',
        'AI reviews every15 minutes, important changes after5 minutes; at most4 reviews/hour. New proposals at most every30 minutes.',
        'Directional skeleton attacks use server paths. Guards choose a sector; redeployment takes30 real seconds. Auto reserves contribute75% of personal strength to directional attacks.',
        'Economic advice uses stock observations. Candles, prices and taxes are not changed automatically. Existing dungeons can be proposed for voluntary participation.',
        'Click the Settings version to read release history. Enemies wait for the countdown; recovery displays real days, hours and minutes.'],
    },
  },
  {
    version: '0.5.3', date: '2026-10-15',
    title: { ru: 'Исправления интерфейса и стройки', en: 'Interface and construction fixes' },
    changes: {
      ru: [
        'Чат больше не закрывает карточку выбранного здания и её кнопки.',
        'Характеристики показывают вашего персонажа. Информация о супруге больше не повторяется в каждой строке.',
        'Подписи зданий привязаны к самому зданию или текущей стадии стройки.',
        'Полезный вклад в стройку резервируется: несколько игроков не могут одновременно занять одну оставшуюся работу.',
        'В заданиях видна стоимость энергии с учётом условий. Исправлены полоса энергии и читаемость статуса в тёмной теме.',
      ],
      en: [
        'Chat no longer covers the selected building card or its buttons.',
        'Attributes show your own character. Spouse information no longer repeats in every row.',
        'Building labels follow the building itself or its current construction stage.',
        'Useful construction work is reserved so several players cannot claim the same remaining work.',
        'Tasks show their energy cost including current conditions. Fixed the energy bar and dark-theme status contrast.',
      ],
    },
  },
  {
    version: '0.5.2', date: '2026-10-15',
    title: { ru: 'Больница и совместная жизнь', en: 'Hospital and village life' },
    changes: {
      ru: [
        'Больница: пациенты, причины ранений, сроки лечения и переноска раненых после боя. Дуэльные ранения связаны с батюшкой.',
        'Мост строится этапами. Добыча за закрытой переправой запрещена сервером; за полезную стройку начисляется слава.',
        'Лимиты ресурсов и улучшения складов, общий жилой дом, заявки на заселение и связь браков с батюшкой.',
        'Истощение замедляет работу, а работа без отдыха может привести к обмороку. Добавлены квалифицированное изготовление энергетиков, ремонт и заявки на разборку.',
        'Батюшка реже повторяется и молчит без свежего разговора. Увеличены зомби и скелеты; подключены единые обводки зданий.',
      ],
      en: [
        'Hospital patients, injury reasons, recovery timers and carrying the wounded after combat. Duel injuries are linked to the chaplain.',
        'Phased bridge construction, server-enforced river access and renown for useful construction work.',
        'Resource-specific storage limits and upgrades, shared housing, settlement requests and linked marriages.',
        'Exhaustion slows work and working without rest can cause collapse. Qualified energy-drink crafting, repairs and demolition requests.',
        'The chaplain repeats less and stays quiet without a recent conversation. Larger zombies and skeletons; consistent building outlines.',
      ],
    },
  },
  {
    version: '0.5.1', date: '2026-10-14',
    title: { ru: 'Компактный чат', en: 'Compact chat' },
    changes: {
      ru: ['Кнопка чата стала компактнее. Жители и онлайн перенесены наверх вкладки «Город».', 'Чат и уведомления уступают место игровым окнам; черновик, история и непрочитанные сохраняются.'],
      en: ['A compact chat button; resident and online counts moved to the Village tab.', 'Chat and notifications yield to game windows while preserving drafts, history and unread counts.'],
    },
  },
  {
    version: '0.5', date: '2026-10-14',
    title: { ru: 'Три режима чата', en: 'Three chat modes' },
    changes: {
      ru: ['Скрытый чат с уведомлениями, чат на четверть экрана и полноэкранный режим.', 'Сохранение черновика, история сообщений, счётчик непрочитанных и отображение версии в настройках.'],
      en: ['Hidden chat with notifications, quarter-screen chat and fullscreen chat.', 'Saved drafts, message history, unread counts and the game version in Settings.'],
    },
  },
]
