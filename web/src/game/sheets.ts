import type { CSSProperties } from 'react'
import { Assets, Rectangle, Spritesheet, Texture, type TextureSource } from 'pixi.js'
import atlas from '../assets/sprites.json'
import atlasUrl from '../assets/sprites.png'

export const TILE = 16

export type SheetName = 'town' | 'dungeon' | 'battle' | 'farm' | 'factory' | 'rogue' | 'extras'

const SHEETS: Record<SheetName, { url: string; columns: number; rows: number }> = {
  town: { url: '/assets/kenney/tiny-town.png', columns: 12, rows: 11 },
  dungeon: { url: '/assets/kenney/tiny-dungeon.png', columns: 12, rows: 11 },
  battle: { url: '/assets/kenney/tiny-battle.png', columns: 18, rows: 11 },
  farm: { url: '/assets/kenney/tiny-farm.png', columns: 12, rows: 11 },
  factory: { url: '/assets/kenney/tiny-factory.png', columns: 12, rows: 11 },
  rogue: { url: '/assets/kenney/roguelike-rpg.png', columns: 57, rows: 31 },
  extras: { url: '/assets/extras.png', columns: 3, rows: 1 },
}

/** A frame of the game's own art, packed by `npm run sprites` from art/generated. */
export type ArtName = keyof typeof atlas.frames
export type AnimationName = keyof typeof atlas.animations

/** A 16 × 16 cell of a Kenney sheet, or a frame of the game's own art (which may be larger than a tile). */
export type TileRef = { sheet: SheetName; index: number } | { art: ArtName }

export const town = (index: number): TileRef => ({ sheet: 'town', index })
export const dungeon = (index: number): TileRef => ({ sheet: 'dungeon', index })
export const battle = (index: number): TileRef => ({ sheet: 'battle', index })
export const farm = (index: number): TileRef => ({ sheet: 'farm', index })
export const factory = (index: number): TileRef => ({ sheet: 'factory', index })
export const rogue = (index: number): TileRef => ({ sheet: 'rogue', index })
export const art = (name: ArtName): TileRef => ({ art: name })
const extras = (index: number): TileRef => ({ sheet: 'extras', index })

/** Tiles of `extras.png`, made for this game in the style of the Kenney sheets. */
export const EXTRAS = { coal: extras(0), campfire: extras(1), feather: extras(2) }

/** Tells two tiles apart, for keys that decide when a view is redrawn. */
export const tileKey = (tile: TileRef) => ('art' in tile ? tile.art : `${tile.sheet}:${tile.index}`)

/** Width and height of a tile in world pixels. */
export function tileSize(tile: TileRef) {
  if (!('art' in tile)) return { width: TILE, height: TILE }
  const { w, h } = atlas.frames[tile.art].frame
  return { width: w / atlas.meta.scale, height: h / atlas.meta.scale }
}

export class TileSet {
  private readonly sources: Record<SheetName, TextureSource>
  private readonly art: Spritesheet
  private readonly cache = new Map<string, Texture>()

  private constructor(sources: Record<SheetName, TextureSource>, art: Spritesheet) {
    this.sources = sources
    this.art = art
  }

  static async load(): Promise<TileSet> {
    const [entries, art] = await Promise.all([
      Promise.all(
        (Object.keys(SHEETS) as SheetName[]).map(async (name) => {
          const texture = await Assets.load<Texture>(SHEETS[name].url)
          texture.source.scaleMode = 'nearest'
          return [name, texture.source] as const
        }),
      ),
      (async () => {
        const texture = await Assets.load<Texture>(atlasUrl)
        texture.source.scaleMode = 'nearest'
        const sheet = new Spritesheet(texture, atlas)
        await sheet.parse()
        return sheet
      })(),
    ])
    return new TileSet(Object.fromEntries(entries) as Record<SheetName, TextureSource>, art)
  }

  get(tile: TileRef): Texture {
    if ('art' in tile) return this.art.textures[tile.art]
    const key = tileKey(tile)
    let texture = this.cache.get(key)
    if (!texture) {
      const { columns } = SHEETS[tile.sheet]
      const frame = new Rectangle((tile.index % columns) * TILE, Math.floor(tile.index / columns) * TILE, TILE, TILE)
      texture = new Texture({ source: this.sources[tile.sheet], frame })
      this.cache.set(key, texture)
    }
    return texture
  }

  /** Frames of an animation, in order. */
  animation(name: AnimationName): Texture[] {
    return this.art.animations[name]
  }
}

/** CSS for showing one tile as an icon in the React interface. */
export function tileStyle(tile: TileRef, scale: number): CSSProperties {
  const common: CSSProperties = { imageRendering: 'pixelated', display: 'inline-block', flexShrink: 0 }
  if ('art' in tile) {
    const { x, y, w, h } = atlas.frames[tile.art].frame
    const size = atlas.meta.size
    scale /= atlas.meta.scale
    return {
      ...common,
      width: w * scale,
      height: h * scale,
      backgroundImage: `url(${atlasUrl})`,
      backgroundSize: `${size.w * scale}px ${size.h * scale}px`,
      backgroundPosition: `${-x * scale}px ${-y * scale}px`,
    }
  }
  const { url, columns, rows } = SHEETS[tile.sheet]
  return {
    ...common,
    width: TILE * scale,
    height: TILE * scale,
    backgroundImage: `url(${url})`,
    backgroundSize: `${columns * TILE * scale}px ${rows * TILE * scale}px`,
    backgroundPosition: `${-(tile.index % columns) * TILE * scale}px ${-Math.floor(tile.index / columns) * TILE * scale}px`,
  }
}
