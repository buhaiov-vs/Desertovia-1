import type { Catalog, Citizen, Structure } from '../api'
import type { Translator } from '../i18n/translator'

/**
 * Explanations of the citizen's energy and rest numbers, with the rules that produce them.
 * Only beds the village has built are named, so the hints do not give away buildings still to come.
 */
export function restHints(citizen: Citizen, catalog: Catalog, structures: Structure[], i18n: Translator, energy: number) {
  const { t } = i18n
  const { rules } = catalog
  const percentOf = (multiplier: number) => i18n.amount(multiplier * 100)
  const percent = percentOf(citizen.regenerationMultiplier)

  const underTrees = citizen.shelterQuality <= rules.underTreesRegeneration + 1e-9
  const shelters = catalog.buildings.filter(
    (building) => building.shelter > 0 && structures.some((structure) => structure.kind === building.kind),
  )
  const bed = underTrees ? undefined : shelters.find((building) => Math.abs(building.shelterQuality - citizen.shelterQuality) < 1e-9)
  const bedName = bed ? i18n.building(bed.kind).name : t('character.inShelter')
  const beds =
    shelters
      .map((building) =>
        t('hint.bed', {
          building: i18n.building(building.kind).name,
          percent: percentOf(building.shelterQuality),
          count: building.shelter * structures.filter((structure) => structure.kind === building.kind).length,
        }),
      )
      .join(', ') || t('hint.noBeds')
  const place = underTrees
    ? t('hint.underTrees', { percent: percentOf(citizen.shelterQuality), beds })
    : t('hint.inShelter', { building: bedName, percent: percentOf(citizen.shelterQuality), beds })

  const hungerFactor = citizen.isHungry ? rules.hungryRegenerationMultiplier : 1
  const raining = underTrees && citizen.regenerationMultiplier < citizen.shelterQuality * hungerFactor - 1e-9
  const breakdown = [
    t('hint.restPlace', { place: underTrees ? t('character.underTrees') : bedName, percent: percentOf(citizen.shelterQuality) }),
    ...(citizen.isHungry ? [t('hint.restHungry', { multiplier: i18n.amount(rules.hungryRegenerationMultiplier) })] : []),
    ...(raining ? [t('hint.restRain', { multiplier: i18n.amount(rules.rainUnderTreesRegeneration) })] : []),
  ].join(' × ')

  const fullPerHour = citizen.maximumEnergy / rules.energyFullRegenerationHours
  const minutesToFull = citizen.energyPerHour > 0 ? Math.ceil(((citizen.maximumEnergy - energy) / citizen.energyPerHour) * 60) : 0
  const perHour = [
    t('hint.perHour', {
      maximum: citizen.maximumEnergy,
      hours: rules.energyFullRegenerationHours,
      fullPerHour: i18n.amount(fullPerHour),
      percent,
      perHour: i18n.amount(citizen.energyPerHour),
    }),
    energy >= citizen.maximumEnergy
      ? t('hint.full')
      : minutesToFull > 0
        ? t('hint.fullIn', { hours: Math.floor(minutesToFull / 60), minutes: minutesToFull % 60 })
        : '',
  ].join('\n')

  return {
    underTrees,
    /** Rest speed in percent of a good bed, as shown to the player. */
    percent,
    place,
    energy: t('hint.energy', {
      energy: Math.floor(energy),
      maximum: citizen.maximumEnergy,
      cost: rules.energyCostPerCycle,
      tiredYield: percentOf(rules.tiredYieldMultiplier),
      base: rules.energyBase,
      perEndurance: rules.energyPerEndurance,
    }),
    perHour,
    restSpeed: t('hint.restSpeed', { breakdown, percent }),
  }
}
