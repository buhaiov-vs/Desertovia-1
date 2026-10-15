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
