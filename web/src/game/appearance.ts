import { appPath } from '../paths'
import type { Appearance, Gender } from '../api'
import { TILE } from './sheets'

const SHEET_URL = appPath('/assets/kenney/tiny-dungeon.png')
const SHEET_COLUMNS = 12

type Layer = 'hair' | 'outfit'
/** Layer and tone (0 is the lightest) a sheet colour is repainted with. */
type Swap = Record<string, [Layer, number]>

/**
 * A Kenney character the look is painted on. The sheet reuses its browns for hair and trousers,
 * so colours are swapped by region: `head` applies down to `split` (inclusive), `body` below it.
 */
interface Body {
  index: number
  split: number
  head: Swap
  body: Swap
  /** Hair highlights the sheet draws in the skin colour, as `[x, y]`. */
  shine?: [number, number][]
}

const HAIR: Swap = { bd6c4a: ['hair', 0], '763b36': ['hair', 1] }

const BODIES: Record<Gender, Body[]> = {
  Male: [
    { index: 85, split: 9, head: HAIR, body: { c0cbdc: ['outfit', 0], '8b9bb4': ['outfit', 1] } },
    { index: 98, split: 9, head: HAIR, body: { c0cbdc: ['outfit', 0], '8b9bb4': ['outfit', 1], '52607c': ['outfit', 2] } },
    { index: 88, split: 9, head: HAIR, body: { bd6c4a: ['outfit', 1], '763b36': ['outfit', 2] } },
    {
      index: 86,
      split: 10,
      head: { ...HAIR, c0cbdc: ['outfit', 0] },
      body: { c0cbdc: ['outfit', 0], '8b9bb4': ['outfit', 1] },
    },
    {
      index: 112,
      split: 10,
      head: { ...HAIR, '43e1b3': ['outfit', 0], '25956a': ['outfit', 1] },
      body: { '43e1b3': ['outfit', 0], '25956a': ['outfit', 1] },
    },
  ],
  Female: [
    {
      index: 99,
      split: 12,
      shine: [[6, 3], [5, 4], [7, 4], [6, 5]],
      head: { ...HAIR, d176d0: ['outfit', 0], '9b4ca3': ['outfit', 1] },
      body: { d176d0: ['outfit', 0], '9b4ca3': ['outfit', 1] },
    },
    {
      index: 100,
      split: 11,
      head: { c0cbdc: ['hair', 0], '8b9bb4': ['hair', 1] },
      body: { bd6c4a: ['outfit', 1], '763b36': ['outfit', 2] },
    },
  ],
}

const SKIN_LIGHT = ['f7c282', 'eaa56c']
const SKIN_SHADE = 'e19a65'

/** Light and shade. The first is the sheet's own colour. */
export const SKIN_TONES: [string, string][] = [
  ['f7c282', 'e19a65'],
  ['ffe0c4', 'f0b89a'],
  ['e8a86a', 'c9804a'],
  ['c99a6b', 'a77748'],
  ['a0663c', '7c4a2a'],
  ['6e4229', '4f2c1c'],
]

/** Light and dark. The first is the sheet's own brown. */
export const HAIR_COLORS: [string, string][] = [
  ['bd6c4a', '763b36'],
  ['5a4a5a', '2e2230'],
  ['8a5536', '5a3424'],
  ['c4483a', '7f2a2a'],
  ['e8823a', 'b0502a'],
  ['f2d27a', 'c99a45'],
  ['c0cbdc', '8b9bb4'],
  ['f4f4f4', 'c8ccd8'],
]

/** Light, middle and dark. Null keeps the clothes the sprite was drawn with. */
export const OUTFIT_COLORS: ([string, string, string] | null)[] = [
  null,
  ['f0817a', 'd04848', '8f2a35'],
  ['8fb8ef', '4f7fd0', '2f4c8f'],
  ['8fe08a', '3f9f4a', '2a6a38'],
  ['d176d0', '9b4ca3', '63306e'],
  ['e0a070', 'bd6c4a', '763b36'],
  ['c0cbdc', '8b9bb4', '52607c'],
  ['fbe08a', 'e0b040', 'a07a2a'],
  ['51474d', '29232a', '15121a'],
]

export const GENDERS: Gender[] = ['Male', 'Female']

export function stylesFor(gender: Gender) {
  return BODIES[gender].length
}

export function appearanceKey(appearance: Appearance) {
  return `${appearance.gender}:${appearance.style}:${appearance.skinTone}:${appearance.hairColor}:${appearance.outfitColor}`
}

export function randomAppearance(): Appearance {
  const pick = (count: number) => Math.floor(Math.random() * count)
  const gender = GENDERS[pick(GENDERS.length)]
  return {
    gender,
    style: pick(stylesFor(gender)),
    skinTone: pick(SKIN_TONES.length),
    hairColor: pick(HAIR_COLORS.length),
    outfitColor: pick(OUTFIT_COLORS.length),
  }
}

const hex = (r: number, g: number, b: number) =>
  ((r << 16) | (g << 8) | b).toString(16).padStart(6, '0')

function rgb(color: string): [number, number, number] {
  const value = parseInt(color, 16)
  return [(value >> 16) & 0xff, (value >> 8) & 0xff, value & 0xff]
}

function lighten(color: string, amount: number) {
  const [r, g, b] = rgb(color).map((channel) => Math.round(channel + (255 - channel) * amount))
  return hex(r, g, b)
}

/** Paints citizens on 16 × 16 canvases from the character sprites of the sheet. */
export class CharacterPainter {
  private readonly pixels: ImageData
  private readonly cache = new Map<string, HTMLCanvasElement>()

  private constructor(pixels: ImageData) {
    this.pixels = pixels
  }

  paint(appearance: Appearance): HTMLCanvasElement {
    const key = appearanceKey(appearance)
    let canvas = this.cache.get(key)
    if (!canvas) {
      canvas = this.render(appearance)
      this.cache.set(key, canvas)
    }
    return canvas
  }

  private render(appearance: Appearance): HTMLCanvasElement {
    const bodies = BODIES[appearance.gender] ?? BODIES.Male
    const body = bodies[appearance.style] ?? bodies[0]
    const skin = SKIN_TONES[appearance.skinTone] ?? SKIN_TONES[0]
    const hair = HAIR_COLORS[appearance.hairColor] ?? HAIR_COLORS[0]
    const outfit = OUTFIT_COLORS[appearance.outfitColor] ?? null

    const canvas = document.createElement('canvas')
    canvas.width = TILE
    canvas.height = TILE
    const context = canvas.getContext('2d')!
    const tile = context.createImageData(TILE, TILE)
    const left = (body.index % SHEET_COLUMNS) * TILE
    const top = Math.floor(body.index / SHEET_COLUMNS) * TILE
    const shine = new Set((body.shine ?? []).map(([x, y]) => y * TILE + x))

    for (let y = 0; y < TILE; y++) {
      for (let x = 0; x < TILE; x++) {
        const from = ((top + y) * this.pixels.width + left + x) * 4
        const to = (y * TILE + x) * 4
        const source = this.pixels.data
        let [r, g, b] = [source[from], source[from + 1], source[from + 2]]
        const color = hex(r, g, b)
        const swap = (y <= body.split ? body.head : body.body)[color]
        let painted: string | null = null
        if (shine.has(y * TILE + x)) painted = lighten(hair[0], 0.35)
        else if (SKIN_LIGHT.includes(color)) painted = skin[0]
        else if (color === SKIN_SHADE) painted = skin[1]
        else if (swap?.[0] === 'hair') painted = hair[Math.min(swap[1], 1)]
        else if (swap?.[0] === 'outfit' && outfit) painted = outfit[swap[1]]
        if (painted) [r, g, b] = rgb(painted)
        tile.data.set([r, g, b, source[from + 3]], to)
      }
    }
    context.putImageData(tile, 0, 0)
    return canvas
  }

  private static loading: Promise<CharacterPainter> | null = null

  static load(): Promise<CharacterPainter> {
    CharacterPainter.loading ??= new Promise<CharacterPainter>((resolve, reject) => {
      const image = new Image()
      image.onload = () => {
        const canvas = document.createElement('canvas')
        canvas.width = image.width
        canvas.height = image.height
        const context = canvas.getContext('2d', { willReadFrequently: true })!
        context.drawImage(image, 0, 0)
        resolve(new CharacterPainter(context.getImageData(0, 0, image.width, image.height)))
      }
      image.onerror = () => {
        CharacterPainter.loading = null
        reject(new Error(`Could not load ${SHEET_URL}`))
      }
      image.src = SHEET_URL
    })
    return CharacterPainter.loading
  }
}
