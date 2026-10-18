import { tileStyle, type TileRef } from '../game/sheets'
import { RESOURCE_ICONS, taskIcon } from '../game/worldMap'

export function TileIcon({ tile, scale = 2, title }: { tile: TileRef | undefined; scale?: number; title?: string }) {
  if (!tile) return <span style={{ width: 16 * scale, height: 16 * scale, display: 'inline-block' }} />
  return <span className="tile-icon" style={tileStyle(tile, scale)} title={title} />
}

export const ResourceIcon = ({ kind, scale = 2, sharpen = 0 }: { kind: string; scale?: number; sharpen?: number }) => (
  <span className="resource-icon">
    <TileIcon tile={RESOURCE_ICONS[kind]} scale={scale} />
    {sharpen > 0 && <span className="sharpen-badge">+{sharpen}</span>}
  </span>
)

export const TaskIcon = ({ taskId, scale = 2 }: { taskId: string; scale?: number }) => (
  <TileIcon tile={taskIcon(taskId)} scale={scale} />
)
