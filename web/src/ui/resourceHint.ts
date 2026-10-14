import { buildingMaterials, discoveredTechnologies, unlockedTasks, type Catalog, type VillageState } from '../api'
import type { Translator } from '../i18n/translator'

/**
 * How much of a good the village holds, where it comes from and what it is for.
 * Only work and buildings the village already has access to are named, so nothing still to come is given away.
 */
export function resourceHint(kind: string, village: VillageState, catalog: Catalog, i18n: Translator) {
  const { t } = i18n
  const name = i18n.resource(kind)
  const stock = village.resources.find((resource) => resource.kind === kind)
  const amount = stock?.amount ?? 0
  const capacity = stock?.capacity ?? null

  const unlocked = unlockedTasks(village, catalog)
  const tasks = catalog.tasks.filter((task) => unlocked.has(task.id))
  const names = (ids: string[]) => [...new Set(ids)].join(', ')
  const sources = names(tasks.filter((task) => task.outputs.some((output) => output.resource === kind)).map((task) => i18n.task(task.id)))

  const discovered = discoveredTechnologies(village)
  const buildings = catalog.buildings.filter(
    (building) =>
      building.stages.length > 0 &&
      (!building.technology || discovered.has(building.technology)) &&
      buildingMaterials(catalog, building, 1).some((material) => material.resource === kind),
  )
  const uses = names([
    ...tasks.filter((task) => task.inputs.some((input) => input.resource === kind)).map((task) => i18n.task(task.id)),
    ...buildings.map((building) => i18n.building(building.kind).name),
  ])

  return [
    capacity === null
      ? t('hint.resourceStockUnlimited', { resource: name, amount: i18n.amount(amount) })
      : t('hint.resourceStock', { resource: name, amount: i18n.amount(amount), capacity: i18n.amount(capacity) }),
    capacity !== null && capacity > 0 && amount >= capacity ? t('hint.resourceFull') : '',
    kind === 'Food' ? t('hint.resourceFood', { cycles: catalog.rules.cyclesPerMeal }) : '',
    sources ? t('hint.resourceFrom', { tasks: sources }) : t('hint.resourceFromNone'),
    uses ? t('hint.resourceUses', { uses }) : '',
  ]
    .filter((line) => line !== '')
    .join('\n')
}
