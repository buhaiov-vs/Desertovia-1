import type { Translator } from '../i18n/translator'

/** "Tsar's title (mastery)": what the Tsar gave the citizen, then the profession their skills earned. */
export function citizenTitle(
  citizen: { subtitle: string | null; isGovernor: boolean; titleRoleId: string },
  i18n: Translator,
) {
  const mastery = i18n.role(citizen.titleRoleId)
  const given = [citizen.isGovernor ? i18n.t('hud.governor') : '', citizen.subtitle ?? ''].filter(Boolean).join(' · ')
  return given ? `${given} (${mastery})` : mastery
}
