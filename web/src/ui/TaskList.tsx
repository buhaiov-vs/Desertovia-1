import { useState } from 'react'
import {
  blockedByLoad,
  fieldsBlock,
  GOLD_VEIN,
  isArmed,
  type Catalog,
  type Citizen,
  type FieldBed,
  type Herd,
  type Resource,
  type Skill,
  type Task,
} from '../api'
import { useI18n, type Translator } from '../i18n'
import type { UiKey } from '../i18n/en'
import { TaskIcon } from './TileIcon'

const FIELD_LOCKS: Record<NonNullable<ReturnType<typeof fieldsBlock>>, UiKey> = {
  'farm.noEmptyBed': 'task.noEmptyBed',
  'farm.nothingToWater': 'task.nothingToWater',
  'farm.nothingRipe': 'task.nothingRipe',
}

function describeOutcome(task: Task, catalog: Catalog, i18n: Translator) {
  const { t } = i18n
  switch (task.effect) {
    case 'Sow': {
      const crop = catalog.crops.find((c) => c.kind === task.crop)
      if (!crop) return ''
      return t('outcome.sow', {
        hours: i18n.amount(crop.growHours),
        harvest: crop.harvest.map((h) => i18n.produced(h.resource, h.amount)).join(', '),
      })
    }
    case 'Water':
      return t('outcome.water')
    case 'Harvest':
      return t('outcome.harvest')
    case 'Morale':
      return t('outcome.morale')
    case 'Deliver':
      return task.outputs
        .map((o) => t('outcome.deliver', { goods: i18n.held(o.resource, o.amount) }))
        .join(', ')
    case 'Survey':
      return t('outcome.survey')
    case 'Build':
      return t('outcome.build')
    case 'Supervise':
      return t('outcome.supervise')
    case 'Expedition':
      return t('outcome.expedition')
    case 'Roam':
      return t('outcome.roam')
    case 'Unload':
      return t('outcome.unload')
    case 'Guard':
      return t('outcome.guard')
    case 'Repair':
      return t('outcome.repair', { amount: i18n.amount(task.baseYield) })
    case 'NotAvailableYet':
      return ''
    default:
      return task.outputs.map((o) => i18n.produced(o.resource, o.amount)).join(', ')
  }
}

/** Work more than this many craft levels below the citizen's teaches only a quarter; mirrors the server. */
const CHALLENGE_THRESHOLD = 3

const skillLevel = (citizen: Citizen, skill: Skill) => citizen.skills.find((s) => s.skill === skill)?.level ?? 0

/** Hunting needs at least one whole animal; mirrors the server. */
const MINIMUM_TO_HUNT = 1
/** Below this share of the herd's capacity the catch gets smaller; mirrors the server. */
const ABUNDANT_SHARE = 0.5

export function TaskCard({
  task,
  citizen,
  catalog,
  built,
  discovered,
  wildlife,
  goldVein,
  fields,
  resources,
  disabled,
  onStart,
}: {
  task: Task
  citizen: Citizen
  catalog: Catalog
  /** Buildings the village has and projects it finished; tasks needing a missing one are locked. */
  built: Set<string>
  /** Technologies the village discovered; tasks and roles needing another one are locked. */
  discovered: Set<string>
  wildlife: Herd[]
  /** Gold still in the vein on the far bank. */
  goldVein: number
  /** Beds of the village farms; farm work needs a bed to do it on. */
  fields: FieldBed[]
  /** Village stock; a task is locked when neither storage nor hands have room for what it produces. */
  resources: Resource[]
  disabled: boolean
  onStart: (task: Task) => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const role = catalog.roles.find((r) => r.id === task.roleId)!
  const craftLevel = skillLevel(citizen, task.skill)
  const mastery = Math.min(Math.max(craftLevel - task.skillLevel, 0), catalog.rules.skillBonusLevels)
  const qualified = craftLevel >= task.skillLevel
  const missingBuilding = task.requiredBuilding && !built.has(task.requiredBuilding) ? task.requiredBuilding : null
  const missingProject = task.requiredProject && !built.has(task.requiredProject) ? task.requiredProject : null
  const missingTechnology =
    [task.requiredTechnology, role.technology].find((id): id is string => !!id && !discovered.has(id)) ?? null
  const emptyHands = task.effect === 'Unload' && citizen.load.length === 0
  const herd = task.hunt ? wildlife.find((h) => h.kind === task.hunt!.prey) : undefined
  const huntedOut = herd !== undefined && herd.count < MINIMUM_TO_HUNT
  const digsVein = task.workplace === GOLD_VEIN
  const veinDug = digsVein && goldVein <= 0
  const fieldsBlocked = missingBuilding === null ? fieldsBlock(task, fields) : null
  const available =
    task.effect !== 'NotAvailableYet' &&
    missingBuilding === null &&
    missingProject === null &&
    missingTechnology === null &&
    !emptyHands &&
    !huntedOut &&
    !veinDug &&
    fieldsBlocked === null
  const tooEasy = craftLevel > task.skillLevel + CHALLENGE_THRESHOLD
  const handsFull = blockedByLoad(task, citizen, resources, catalog.rules.takeLimit)
  const isCurrent = citizen.taskId === task.id && citizen.state !== 'Idle'
  const name = i18n.task(task.id)
  const outcome = describeOutcome(task, catalog, i18n)
  const requirements = `${i18n.skill(task.skill)} ${task.skillLevel}`
  const inputs = task.inputs.map((input) => `${input.amount} ${i18n.resource(input.resource).toLowerCase()}`).join(', ')

  return (
    <button
      className={`task-card ${isCurrent ? 'current' : ''}`}
      disabled={disabled || !qualified || !available || handsFull !== null}
      onClick={() => onStart(task)}
    >
      <TaskIcon taskId={task.id} scale={2} />
      <span className="task-text">
        <span className="task-name">
          {isCurrent && citizen.state === 'AwaitingRenewal' ? t('task.again', { task: name }) : name}
        </span>
        <span className="task-meta">
          {i18n.role(role.id)} · {t('task.gives', { stat: i18n.stat(task.primary) })} ·{' '}
          {t('task.trains', { skill: i18n.skill(task.skill) })}
          {outcome && ` · ${outcome}`}
          {inputs && ` · ${t('task.uses', { inputs })}`}
        </span>
        {mastery > 0 && (
          <span className="task-meta">
            {t('task.mastery', {
              skill: i18n.skill(task.skill),
              level: craftLevel,
              yield: Math.round(mastery * catalog.rules.skillYieldPerLevel * 100),
              time: Math.round(mastery * catalog.rules.skillWorkTimePerLevel * 100),
            })}
          </span>
        )}
        {task.hunt && (
          <span className="task-meta">
            {t('task.huntTime', {
              seconds: task.workSeconds,
              experience: Number((task.workSeconds / catalog.rules.cycleSeconds).toFixed(2)),
            })}
          </span>
        )}
        {herd && (
          <span className="task-meta">
            {t('task.prey', { animal: i18n.animal(herd.kind), count: herd.count, capacity: herd.capacity })}
          </span>
        )}
        {task.danger && (
          <span className="task-meta task-danger">
            {t('task.danger', {
              enemies: task.danger.enemies.map((kind) => i18n.enemy(kind).toLowerCase()).join(', '),
            })}
          </span>
        )}
        {isArmed(task) && <span className="task-meta">{t('task.armed')}</span>}
        {!qualified && <span className="task-lock">{t('task.requires', { requirements })}</span>}
        {qualified && missingBuilding && (
          <span className="task-lock">{t('task.needsBuilding', { building: i18n.building(missingBuilding).name })}</span>
        )}
        {qualified && missingProject && (
          <span className="task-lock">{t('task.needsBuilding', { building: i18n.project(missingProject).name })}</span>
        )}
        {missingTechnology && (
          <span className="task-lock">
            {t('task.needsTechnology', { technology: i18n.technology(missingTechnology).name })}
          </span>
        )}
        {qualified && emptyHands && <span className="task-lock">{t('task.emptyHands')}</span>}
        {digsVein && !veinDug && (
          <span className="task-meta">{t('task.vein', { amount: i18n.amount(goldVein) })}</span>
        )}
        {qualified && huntedOut && <span className="task-lock">{t('task.preyGone')}</span>}
        {qualified && veinDug && <span className="task-lock">{t('task.veinEmpty')}</span>}
        {qualified && fieldsBlocked && <span className="task-lock">{t(FIELD_LOCKS[fieldsBlocked])}</span>}
        {qualified &&
          !available &&
          !missingBuilding &&
          !missingProject &&
          !missingTechnology &&
          !emptyHands &&
          !huntedOut &&
          !veinDug &&
          !fieldsBlocked && (
          <span className="task-lock">{t('task.soon')}</span>
        )}
        {qualified && available && herd && herd.count < herd.capacity * ABUNDANT_SHARE && (
          <span className="task-warning">{t('task.preyScarce')}</span>
        )}
        {qualified && available && handsFull && (
          <span className="task-lock">{t('task.handsFull', { resource: i18n.resource(handsFull).toLowerCase() })}</span>
        )}
        {qualified && available && !handsFull && tooEasy && <span className="task-warning">{t('task.tooEasy')}</span>}
      </span>
    </button>
  )
}

type CardProps = Omit<Parameters<typeof TaskCard>[0], 'task'>

const QUESTION_MARK = ['.###.', '#...#', '....#', '..##.', '..#..', '.....', '..#..']

/** A question mark drawn in the same chunky pixels as the game's tiles. */
function PixelQuestion({ size }: { size: number }) {
  return (
    <svg className="pixel-question" width={size * 5} height={size * 7} viewBox="0 0 5 7" shapeRendering="crispEdges" aria-hidden>
      {QUESTION_MARK.flatMap((row, y) =>
        [...row].map((cell, x) => (cell === '#' ? <rect key={`${x},${y}`} x={x} y={y} width={1} height={1} /> : null)),
      )}
    </svg>
  )
}

/** How one family of tasks (prey to hunt, seeds to sow) is offered on a single card. */
interface Variants {
  /** Label of the chip group. */
  group: UiKey
  /** Name of a variant the citizen is not skilled enough for. */
  unknown: UiKey
  name: (task: Task) => string
  /** What the citizen notices of a variant they cannot do yet: a riddle, not a name. */
  hint: (task: Task) => string
  /** Lock text of an unknown variant: the craft level it needs. */
  locked: (task: Task) => string
}

/**
 * One card for a family of tasks: the player picks the variant, each with its own outcome and skill level.
 * Variants the citizen is not skilled enough for stay a mystery: a question mark and a hint, never a name.
 */
function VariantCard({ tasks, variants, ...props }: CardProps & { tasks: Task[]; variants: Variants }) {
  const { t } = useI18n()
  const { citizen } = props
  const known = (task: Task) => skillLevel(citizen, task.skill) >= task.skillLevel
  const current = tasks.find((task) => task.id === citizen.taskId)
  const [chosenId, setChosenId] = useState<string | null>(null)
  const chosen = tasks.find((task) => task.id === (chosenId ?? current?.id)) ?? tasks.find(known) ?? tasks[0]

  return (
    <div className="pick-card">
      <div className="chips" role="group" aria-label={t(variants.group)}>
        {tasks.map((task) => (
          <button
            key={task.id}
            className={`chip ${task.id === chosen.id ? 'active' : ''} ${known(task) ? '' : 'unknown'}`}
            aria-pressed={task.id === chosen.id}
            aria-label={known(task) ? undefined : t(variants.unknown)}
            onClick={() => setChosenId(task.id)}
          >
            {known(task) ? variants.name(task) : <PixelQuestion size={2} />}
          </button>
        ))}
      </div>
      {known(chosen) ? (
        <TaskCard task={chosen} {...props} />
      ) : (
        <button className="task-card mystery" disabled>
          <PixelQuestion size={5} />
          <span className="task-text">
            <span className="task-name">{t(variants.unknown)}</span>
            <span className="task-meta">{variants.hint(chosen)}</span>
            <span className="task-lock">{variants.locked(chosen)}</span>
          </span>
        </button>
      )}
    </div>
  )
}

export function TaskList({ tasks, ...props }: CardProps & { tasks: Task[] }) {
  const i18n = useI18n()
  const { t } = i18n
  const hunts = tasks.filter((task) => task.hunt !== null)
  const seeds = tasks.filter((task) => task.crop !== null)
  const locked = (task: Task) => t('task.variantLocked', { skill: i18n.skill(task.skill), level: task.skillLevel })
  return (
    <div className="task-grid">
      {tasks.map((task) => {
        if (task === hunts[0]) {
          return (
            <VariantCard
              key="hunt"
              tasks={hunts}
              variants={{
                group: 'task.huntPrey',
                unknown: 'task.preyUnknown',
                name: (hunt) => i18n.animal(hunt.hunt!.prey),
                hint: (hunt) => i18n.animalHint(hunt.hunt!.prey),
                locked,
              }}
              {...props}
            />
          )
        }
        if (task === seeds[0]) {
          return (
            <VariantCard
              key="sow"
              tasks={seeds}
              variants={{
                group: 'task.chooseSeed',
                unknown: 'task.seedUnknown',
                name: (seed) => i18n.crop(seed.crop!),
                hint: (seed) => i18n.cropHint(seed.crop!),
                locked,
              }}
              {...props}
            />
          )
        }
        if (task.hunt !== null || task.crop !== null) return null
        return <TaskCard key={task.id} task={task} {...props} />
      })}
    </div>
  )
}
