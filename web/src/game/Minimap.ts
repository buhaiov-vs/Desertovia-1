import { farTerrain, LAND_HEIGHT, LAND_LEFT, LAND_TOP, LAND_WIDTH, MAP_HEIGHT, MAP_LEFT, MAP_TOP, MAP_WIDTH, Terrain } from './worldMap'

const TERRAIN_COLORS: Record<Terrain, [number, number, number]> = {
  [Terrain.Grass]: [126, 200, 80],
  [Terrain.Water]: [79, 184, 232],
  [Terrain.Tree]: [47, 107, 42],
  [Terrain.Stone]: [156, 156, 156],
  [Terrain.Bush]: [140, 70, 60],
  [Terrain.Reeds]: [156, 138, 58],
  [Terrain.Clearing]: [176, 150, 100],
  [Terrain.Mountain]: [104, 96, 92],
  [Terrain.Ravine]: [52, 38, 28],
  [Terrain.Thicket]: [36, 80, 31],
  [Terrain.Rockfall]: [130, 122, 114],
  [Terrain.Marsh]: [95, 122, 69],
  [Terrain.Cave]: [28, 23, 18],
  [Terrain.Lookout]: [200, 190, 175],
  [Terrain.Trail]: [156, 127, 90],
}

const FOG: [number, number, number] = [12, 14, 20]

export interface MinimapMarkers {
  buildings: Array<{ x: number; y: number; width: number; height: number; finished: boolean }>
  citizens: Array<{ x: number; y: number; kind: 'you' | 'tsar' | 'citizen' }>
  camp: { x: number; y: number }
}

interface Rectangle {
  x: number
  y: number
  width: number
  height: number
}

type DarknessFill = (x: number, y: number, width: number, height: number, out: Float32Array) => void

const VALE: Rectangle = { x: MAP_LEFT, y: MAP_TOP, width: MAP_WIDTH, height: MAP_HEIGHT }

/**
 * Overview in the corner. While the screen stays inside the camp, this is the whole camp picture.
 * When the screen reaches past that picture, it follows the camera across the same number of tiles,
 * so the player, the fog and the white view stay visible. A click or drag moves the camera there.
 */
export class Minimap {
  private readonly root = document.createElement('div')
  private readonly base = document.createElement('canvas')
  private readonly overlay = document.createElement('canvas')
  private readonly localFog = document.createElement('canvas')
  private readonly darkness = new Float32Array(MAP_WIDTH * MAP_HEIGHT)
  private view: Rectangle = { x: 0, y: 0, width: 0, height: 0 }
  private frame: Rectangle = VALE
  private builtFrame = ''
  private dragging = false
  private markers: MinimapMarkers | null = null
  private terrain: Uint8Array | null = null
  private seed = 1
  /** Fog of war for the camp picture, one pixel per tile. */
  private fog: HTMLCanvasElement | null = null
  private fillDarkness: DarknessFill | null = null

  constructor(host: HTMLElement, onPick: (tileX: number, tileY: number) => void) {
    this.root.className = 'minimap'
    this.base.width = MAP_WIDTH
    this.base.height = MAP_HEIGHT
    this.overlay.width = MAP_WIDTH * 4
    this.overlay.height = MAP_HEIGHT * 4
    this.localFog.width = MAP_WIDTH
    this.localFog.height = MAP_HEIGHT
    this.root.append(this.base, this.overlay)
    host.appendChild(this.root)

    const pick = (event: PointerEvent) => {
      const bounds = this.root.getBoundingClientRect()
      const frame = this.frame
      const tileX = frame.x + ((event.clientX - bounds.left) / bounds.width) * frame.width
      const tileY = frame.y + ((event.clientY - bounds.top) / bounds.height) * frame.height
      onPick(tileX, tileY)
    }
    this.root.addEventListener('pointerdown', (event) => {
      event.stopPropagation()
      this.dragging = true
      this.root.setPointerCapture(event.pointerId)
      pick(event)
    })
    this.root.addEventListener('pointermove', (event) => {
      if (this.root.hasPointerCapture(event.pointerId)) pick(event)
    })
    const release = () => {
      if (!this.dragging) return
      this.dragging = false
      this.frame = this.frameFor(this.view)
      this.draw()
    }
    this.root.addEventListener('pointerup', release)
    this.root.addEventListener('pointercancel', release)
    this.root.addEventListener('wheel', (event) => event.stopPropagation())
  }

  destroy() {
    this.root.remove()
  }

  /** `seed` picks the far land past the camp picture, the same one the world draws. */
  setTerrain(terrain: Uint8Array, seed: number) {
    this.terrain = terrain
    this.seed = seed
    this.builtFrame = ''
    this.draw()
  }

  /** The camp canvas is read on every redraw. `fill` paints darkness for a window past that picture. */
  setFog(fog: HTMLCanvasElement, fill?: DarknessFill) {
    this.fog = fog
    this.fillDarkness = fill ?? null
    this.builtFrame = ''
    this.draw()
  }

  setView(view: Rectangle) {
    this.view = view
    if (!this.dragging) this.frame = this.frameFor(view)
    this.draw()
  }

  setMarkers(markers: MinimapMarkers) {
    this.markers = markers
    // Sight moves with the walkers, so a followed window refreshes its fog with them.
    if (!this.isVale(this.frame)) this.builtFrame = ''
    this.draw()
  }

  /** The camp picture while the screen is inside it; otherwise the same-sized window around the camera. */
  private frameFor(view: Rectangle): Rectangle {
    const covers =
      view.x >= MAP_LEFT &&
      view.y >= MAP_TOP &&
      view.x + view.width <= MAP_LEFT + MAP_WIDTH &&
      view.y + view.height <= MAP_TOP + MAP_HEIGHT
    if (covers) return VALE
    const centerX = view.x + view.width / 2
    const centerY = view.y + view.height / 2
    const x = Math.round(centerX - MAP_WIDTH / 2)
    const y = Math.round(centerY - MAP_HEIGHT / 2)
    return {
      x: Math.max(LAND_LEFT, Math.min(x, LAND_LEFT + LAND_WIDTH - MAP_WIDTH)),
      y: Math.max(LAND_TOP, Math.min(y, LAND_TOP + LAND_HEIGHT - MAP_HEIGHT)),
      width: MAP_WIDTH,
      height: MAP_HEIGHT,
    }
  }

  private isVale(frame: Rectangle) {
    return frame.x === VALE.x && frame.y === VALE.y && frame.width === VALE.width && frame.height === VALE.height
  }

  private paintBase() {
    const key = `${this.frame.x},${this.frame.y}`
    if (key === this.builtFrame) return
    this.builtFrame = key
    const terrain = this.terrain
    const context = this.base.getContext('2d')
    if (!context || !terrain) return
    const frame = this.frame
    if (this.isVale(frame)) {
      const image = context.createImageData(MAP_WIDTH, MAP_HEIGHT)
      terrain.forEach((kind, index) => {
        const [red, green, blue] = TERRAIN_COLORS[kind as Terrain] ?? TERRAIN_COLORS[Terrain.Grass]
        image.data.set([red, green, blue, 255], index * 4)
      })
      context.putImageData(image, 0, 0)
      return
    }
    const image = context.createImageData(frame.width, frame.height)
    for (let row = 0; row < frame.height; row++) {
      const tileY = frame.y + row
      for (let column = 0; column < frame.width; column++) {
        const tileX = frame.x + column
        const kind =
          tileX >= MAP_LEFT && tileX < MAP_LEFT + MAP_WIDTH && tileY >= MAP_TOP && tileY < MAP_TOP + MAP_HEIGHT
            ? (terrain[(tileY - MAP_TOP) * MAP_WIDTH + (tileX - MAP_LEFT)] as Terrain)
            : farTerrain(tileX, tileY, this.seed)
        const [red, green, blue] = TERRAIN_COLORS[kind] ?? TERRAIN_COLORS[Terrain.Grass]
        image.data.set([red, green, blue, 255], (row * frame.width + column) * 4)
      }
    }
    context.putImageData(image, 0, 0)
    this.paintLocalFog()
  }

  private paintLocalFog() {
    const fill = this.fillDarkness
    const context = this.localFog.getContext('2d')
    if (!fill || !context) return
    const frame = this.frame
    fill(frame.x, frame.y, frame.width, frame.height, this.darkness)
    const image = context.createImageData(frame.width, frame.height)
    for (let index = 0; index < frame.width * frame.height; index++) {
      const alpha = Math.round(this.darkness[index] * 255)
      image.data.set([FOG[0], FOG[1], FOG[2], alpha], index * 4)
    }
    context.putImageData(image, 0, 0)
  }

  private draw() {
    this.paintBase()
    const context = this.overlay.getContext('2d')
    if (!context) return
    const frame = this.frame
    const unit = this.overlay.width / frame.width
    const xOf = (tile: number) => (tile - frame.x) * unit
    const yOf = (tile: number) => (tile - frame.y) * unit
    context.clearRect(0, 0, this.overlay.width, this.overlay.height)
    context.imageSmoothingEnabled = true
    if (this.isVale(frame) && this.fog) context.drawImage(this.fog, 0, 0, this.overlay.width, this.overlay.height)
    else if (!this.isVale(frame)) context.drawImage(this.localFog, 0, 0, this.overlay.width, this.overlay.height)

    const markers = this.markers
    if (markers) {
      for (const building of markers.buildings) {
        if (!this.overlaps(building)) continue
        context.fillStyle = building.finished ? '#f4d35e' : 'rgba(244, 211, 94, 0.45)'
        context.strokeStyle = '#3a2a1a'
        context.lineWidth = 1
        context.fillRect(xOf(building.x), yOf(building.y), Math.max(unit, building.width * unit), Math.max(unit, building.height * unit))
        context.strokeRect(xOf(building.x), yOf(building.y), Math.max(unit, building.width * unit), Math.max(unit, building.height * unit))
      }
      if (this.inside(markers.camp.x, markers.camp.y)) {
        context.fillStyle = '#ff7b1c'
        context.beginPath()
        context.arc(xOf(markers.camp.x), yOf(markers.camp.y), unit * 1.2, 0, Math.PI * 2)
        context.fill()
      }
      const order = ['citizen', 'you', 'tsar'] as const
      for (const kind of order) {
        for (const citizen of markers.citizens) {
          if (citizen.kind !== kind || !this.inside(citizen.x, citizen.y)) continue
          const radius = citizen.kind === 'citizen' ? unit * 0.9 : unit * 1.35
          context.fillStyle = citizen.kind === 'citizen' ? '#ffffff' : citizen.kind === 'you' ? '#ffe066' : '#ffd35e'
          context.strokeStyle = '#1b1206'
          context.lineWidth = 1
          context.beginPath()
          context.arc(xOf(citizen.x), yOf(citizen.y), radius, 0, Math.PI * 2)
          context.fill()
          context.stroke()
        }
      }
      this.markOutside(context, markers.camp.x, markers.camp.y, '#ff7b1c')
      const you = markers.citizens.find((citizen) => citizen.kind === 'you') ?? markers.citizens.find((citizen) => citizen.kind === 'tsar')
      if (you) this.markOutside(context, you.x, you.y, you.kind === 'you' ? '#ffe066' : '#ffd35e')
    }

    context.strokeStyle = 'rgba(0, 0, 0, 0.7)'
    context.lineWidth = 4
    context.strokeRect(xOf(this.view.x), yOf(this.view.y), this.view.width * unit, this.view.height * unit)
    context.strokeStyle = '#ffffff'
    context.lineWidth = 2
    context.strokeRect(xOf(this.view.x), yOf(this.view.y), this.view.width * unit, this.view.height * unit)
  }

  private inside(tileX: number, tileY: number) {
    const frame = this.frame
    return tileX >= frame.x && tileX < frame.x + frame.width && tileY >= frame.y && tileY < frame.y + frame.height
  }

  private overlaps(area: { x: number; y: number; width: number; height: number }) {
    const frame = this.frame
    return area.x < frame.x + frame.width && area.x + area.width > frame.x && area.y < frame.y + frame.height && area.y + area.height > frame.y
  }

  /** A dot on the near edge when a place is outside the pictured tiles. */
  private markOutside(context: CanvasRenderingContext2D, tileX: number, tileY: number, color: string) {
    const frame = this.frame
    if (this.inside(tileX, tileY)) return
    const dx = tileX - (frame.x + frame.width / 2)
    const dy = tileY - (frame.y + frame.height / 2)
    const scale = Math.max(Math.abs(dx) / (frame.width / 2), Math.abs(dy) / (frame.height / 2), 0.001)
    const inset = 3
    const edgeX = Math.max(frame.x + inset, Math.min(frame.x + frame.width - inset, frame.x + frame.width / 2 + dx / scale))
    const edgeY = Math.max(frame.y + inset, Math.min(frame.y + frame.height - inset, frame.y + frame.height / 2 + dy / scale))
    const unit = this.overlay.width / frame.width
    context.fillStyle = color
    context.strokeStyle = '#1b1206'
    context.lineWidth = 2
    context.beginPath()
    context.arc((edgeX - frame.x) * unit, (edgeY - frame.y) * unit, unit * 1.6, 0, Math.PI * 2)
    context.fill()
    context.stroke()
  }
}
