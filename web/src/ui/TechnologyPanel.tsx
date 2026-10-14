import { useMemo, useState } from 'react'
import {
  builtKinds,
  type Catalog,
  type Citizen,
  type TechnologyDefinition,
  type TechnologyState,
  type VillageState,
} from '../api'
import { useI18n } from '../i18n'
import { BUILDING_SIGNS, taskIcon } from '../game/worldMap'
import { TileIcon } from './TileIcon'

const NODE_WIDTH = 156
const NODE_HEIGHT = 48
const COLUMN_GAP = 44
const ROW_GAP = 12

interface TreeNode {
  technology: TechnologyDefinition
  x: number
  y: number
}

/**
 * Columns by dependency depth: a technology sits one column right of its deepest prerequisite. Within a column each
 * technology is pulled towards the average row of its prerequisites so links stay short.
 */
function layoutTree(technologies: TechnologyDefinition[]) {
  const byId = new Map(technologies.map((technology) => [technology.id, technology]))
  const depths = new Map<string, number>()
  const depth = (technology: TechnologyDefinition): number => {
    const known = depths.get(technology.id)
    if (known !== undefined) return known
    depths.set(technology.id, 0)
    const parents = technology.requires.map((id) => byId.get(id)).filter((parent) => parent !== undefined)
    const value = parents.length === 0 ? 0 : 1 + Math.max(...parents.map(depth))
    depths.set(technology.id, value)
    return value
  }
  technologies.forEach(depth)

  const columns: TechnologyDefinition[][] = []
  for (const technology of technologies) (columns[depths.get(technology.id)!] ??= []).push(technology)

  const rows = new Map<string, number>()
  columns.forEach((column, index) => {
    const desired = column.map((technology) => {
      const parentRows = technology.requires.map((id) => rows.get(id)).filter((row) => row !== undefined)
      const row = index === 0 || parentRows.length === 0 ? 0 : parentRows.reduce((a, b) => a + b, 0) / parentRows.length
      return { technology, row }
    })
    if (index > 0) desired.sort((a, b) => a.row - b.row)
    let next = 0
    for (const { technology, row } of desired) {
      const placed = index === 0 ? next : Math.max(row, next)
      rows.set(technology.id, placed)
      next = placed + 1
    }
  })

  const nodes = new Map<string, TreeNode>()
  columns.forEach((column, index) =>
    column.forEach((technology) =>
      nodes.set(technology.id, {
        technology,
        x: index * (NODE_WIDTH + COLUMN_GAP),
        y: rows.get(technology.id)! * (NODE_HEIGHT + ROW_GAP),
      }),
    ),
  )
  const lastRow = Math.max(0, ...rows.values())
  return {
    nodes,
    width: columns.length * (NODE_WIDTH + COLUMN_GAP) - COLUMN_GAP,
    height: lastRow * (NODE_HEIGHT + ROW_GAP) + NODE_HEIGHT,
  }
}

/**
 * Discoveries as a tree of what builds on what. The Tsar or a Governor calls a vote on the next one and plans the buildings the research needs;
 * citizens research the chosen one at its building.
 */
export function TechnologyPanel({
  catalog,
  village,
  citizen,
  ruler,
  disabled,
  onPoll,
  onResearch,
}: {
  catalog: Catalog
  village: VillageState
  citizen: Citizen | null | undefined
  /** The Tsar, or a citizen the Tsar appointed Governor: calls votes. */
  ruler: boolean
  disabled: boolean
  /** Opens the open vote, or lets the rulers call one. */
  onPoll: () => void
  onResearch: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const built = builtKinds(village, catalog)
  const states = new Map(village.technologies.map((state) => [state.id, state]))
  const current = village.currentResearch
  const pollOpen = village.poll?.closedAt === null
  const tree = useMemo(() => layoutTree(catalog.technologies), [catalog.technologies])
  const [picked, setPicked] = useState<string | null>(null)
  const selectedId =
    picked ??
    current ??
    village.technologies.find((state) => state.available)?.id ??
    catalog.technologies[0]?.id
  const selected = selectedId ? tree.nodes.get(selectedId)?.technology : undefined

  return (
    <>
      <section className="card">
        <h3>{t('tech.era', { era: i18n.era(village.era) })}</h3>
        <p className="small">{ruler ? t('tech.rulerHint') : t('tech.intro')}</p>
        <p className="small">
          {current ? t('tech.current', { technology: i18n.technology(current).name }) : t('tech.none')}
        </p>
        {(pollOpen || ruler) && (
          <button className="primary" onClick={onPoll}>
            {pollOpen ? t('poll.open') : t('poll.call')}
          </button>
        )}
      </section>

      <section className="card">
        <h3>{t('tech.tree')}</h3>
        <p className="small muted">{t('tech.treeHint')}</p>
        <div className="tech-tree-scroll">
          <div className="tech-tree" style={{ width: tree.width, height: tree.height }}>
            <svg className="tech-links" width={tree.width} height={tree.height} aria-hidden="true">
              {[...tree.nodes.values()].flatMap((node) =>
                node.technology.requires.map((parentId) => {
                  const parent = tree.nodes.get(parentId)
                  if (!parent) return null
                  const x1 = parent.x + NODE_WIDTH
                  const y1 = parent.y + NODE_HEIGHT / 2
                  const x2 = node.x
                  const y2 = node.y + NODE_HEIGHT / 2
                  const bend = Math.min(COLUMN_GAP, (x2 - x1) / 2)
                  const done = states.get(parentId)?.discovered ?? false
                  const active = parentId === selectedId || node.technology.id === selectedId
                  return (
                    <path
                      key={`${parentId}-${node.technology.id}`}
                      className={`tech-link ${done ? 'done' : ''} ${active ? 'active' : ''}`}
                      d={`M ${x1} ${y1} C ${x1 + bend} ${y1}, ${x2 - bend} ${y2}, ${x2} ${y2}`}
                    />
                  )
                }),
              )}
            </svg>
            {[...tree.nodes.values()].map(({ technology, x, y }) => {
              const state = states.get(technology.id)
              const status = state?.discovered
                ? 'discovered'
                : technology.id === current
                  ? 'current'
                  : state?.available
                    ? 'available'
                    : 'locked'
              const progress = !state?.discovered && state && state.done > 0 ? state.done / state.required : null
              return (
                <button
                  key={technology.id}
                  className={`tech-node ${status} ${technology.id === selectedId ? 'selected' : ''}`}
                  style={{ left: x, top: y, width: NODE_WIDTH, height: NODE_HEIGHT }}
                  aria-pressed={technology.id === selectedId}
                  onClick={() => setPicked(technology.id)}
                >
                  <TileIcon tile={BUILDING_SIGNS[technology.researchedAt] ?? taskIcon('research-technology')} scale={2} />
                  <span className="tech-node-text">
                    <span className="tech-node-name">{i18n.technology(technology.id).name}</span>
                    {progress !== null ? (
                      <span className="bar gold thin">
                        <span style={{ width: `${Math.min(100, progress * 100)}%` }} />
                      </span>
                    ) : (
                      <span className="tech-node-meta">
                        {state?.discovered ? t('tech.discovered') : i18n.era(technology.era)}
                      </span>
                    )}
                  </span>
                </button>
              )
            })}
          </div>
        </div>
      </section>

      {selected && (
        <section className="card tech-details">
          <TechnologyCard
            technology={selected}
            state={states.get(selected.id)}
            catalog={catalog}
            citizen={citizen}
            current={selected.id === current}
            hasBuilding={built.has(selected.researchedAt)}
            disabled={disabled}
            onResearch={onResearch}
          />
        </section>
      )}
    </>
  )
}

function TechnologyCard({
  technology,
  state,
  catalog,
  citizen,
  current,
  hasBuilding,
  disabled,
  onResearch,
}: {
  technology: TechnologyDefinition
  state: TechnologyState | undefined
  catalog: Catalog
  citizen: Citizen | null | undefined
  current: boolean
  hasBuilding: boolean
  disabled: boolean
  onResearch: () => void
}) {
  const i18n = useI18n()
  const { t } = i18n
  const { name, description } = i18n.technology(technology.id)
  const discovered = state?.discovered ?? false
  const available = state?.available ?? false
  const building = i18n.building(technology.researchedAt).name
  const level = (stat: string) => citizen?.stats.find((s) => s.stat === stat)?.level ?? 0
  const qualified = technology.researcher.every((r) => level(r.stat) >= r.minimum)
  const researching = citizen?.technologyId === technology.id && citizen.state !== 'Idle'
  const unlocks = [
    ...catalog.buildings.filter((b) => b.technology === technology.id).map((b) => i18n.building(b.kind).name),
    ...catalog.roles.filter((r) => r.technology === technology.id).map((r) => i18n.role(r.id)),
    ...catalog.tasks.filter((task) => task.requiredTechnology === technology.id).map((task) => i18n.task(task.id)),
    ...technology.bonuses.map((bonus) =>
      t('tech.bonus', { task: i18n.task(bonus.taskId), multiplier: i18n.amount(bonus.multiplier) }),
    ),
  ]

  return (
    <div className={`task-card ${current ? 'current' : ''}`}>
      <TileIcon tile={BUILDING_SIGNS[technology.researchedAt] ?? taskIcon('research-technology')} scale={2} />
      <span className="task-text">
        <span className="task-name">
          {name}
          {discovered && <span className="muted small"> · {t('tech.discovered')}</span>}
        </span>
        <span className="task-meta">{description}</span>
        <span className="task-meta">{t('tech.at', { building })}</span>
        <span className="task-meta">{t('tech.researchers', { requirements: i18n.requirements(technology.researcher) })}</span>
        {unlocks.length > 0 && <span className="task-meta">{t('tech.unlocks', { items: unlocks.join(', ') })}</span>}
        {!discovered && !available && technology.requires.length > 0 && (
          <span className="task-lock">
            {t('tech.needs', { technologies: technology.requires.map((id) => i18n.technology(id).name).join(', ') })}
          </span>
        )}
        {!discovered && state && state.done > 0 && (
          <>
            <span className="task-meta" title={t('tech.crewHint', { needed: state.crewNeeded })}>
              {t('tech.progress', { done: i18n.amount(state.done), required: i18n.amount(state.required) })} ·{' '}
              {t('tech.hands', { hands: state.hands, needed: state.crewNeeded })}
            </span>
            <div className="bar gold">
              <div style={{ width: `${Math.min(100, (state.done / state.required) * 100)}%` }} />
            </div>
          </>
        )}
        {available && current && !hasBuilding && (
          <span className="task-lock">{t('tech.needsBuilding', { building })}</span>
        )}
        {citizen && available && current && (
          <span className="task-actions">
            <button
              className="small-button primary"
              disabled={disabled || researching || !qualified || !hasBuilding}
              onClick={onResearch}
            >
              {t('tech.research')}
            </button>
          </span>
        )}
        {citizen && available && current && !qualified && (
          <span className="task-lock">{t('tech.skill', { requirements: i18n.requirements(technology.researcher) })}</span>
        )}
      </span>
    </div>
  )
}
