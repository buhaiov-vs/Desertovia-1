import type { UiKey } from '../i18n/en'

/** Village is shared with everyone, character and tasks are the player's own, settings are not part of the game. */
export type MenuTab = 'village' | 'character' | 'tasks' | 'build' | 'technologies' | 'settings'

export const TABS: Record<MenuTab, UiKey> = {
  village: 'tab.village',
  character: 'tab.character',
  tasks: 'tab.tasks',
  build: 'tab.build',
  technologies: 'tab.technologies',
  settings: 'tab.settings',
}

/**
 * Citizens work and research discoveries; the Tsar (village owner) never works. The rulers, the Tsar and the
 * Governors the Tsar appointed among the citizens, build and call votes on discoveries.
 */
export const tabsFor = (tsar: boolean, ruler: boolean): MenuTab[] =>
  tsar
    ? ['village', 'build', 'technologies', 'settings']
    : ruler
      ? ['village', 'character', 'tasks', 'build', 'technologies', 'settings']
      : ['village', 'character', 'tasks', 'technologies', 'settings']
