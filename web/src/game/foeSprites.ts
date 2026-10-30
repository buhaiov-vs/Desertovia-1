import { AnimatedSprite, Assets, Container, Spritesheet, type Texture } from 'pixi.js'
import atlas from '../assets/foes/foes.json'
import atlasUrl from '../assets/foes/foes.png'

/** An animation of the archive atlas packed by `npm run foes`. */
export type FoeAnimation = keyof typeof atlas.animations

export type FoeLook = {
  idle: FoeAnimation
  /** Played while the figure moves; without it the idle loop keeps playing. */
  walk?: FoeAnimation
  /** Frames per second of the loop. */
  fps?: number
} & (
  | {
      /** Drawn height in world pixels, shadow included. */
      height: number
    }
  | {
      /** World pixels per atlas pixel, for a set of figures whose frames differ in height. */
      scale: number
    }
)

const DEFAULT_FPS = 8
/** Atlas frames are drawn at about 60 frames per second of the shared ticker. */
const TICKS_PER_SECOND = 60

let sheet: Spritesheet | null = null
let loading: Promise<void> | null = null

/** Loads the atlas once. Figures made before it is ready fall back to the code-drawn look. */
export function loadFoeSprites(): Promise<void> {
  loading ??= (async () => {
    const texture = await Assets.load<Texture>(atlasUrl)
    texture.source.scaleMode = 'nearest'
    const loaded = new Spritesheet(texture, atlas)
    await loaded.parse()
    sheet = loaded
  })()
  return loading
}

export const foeSpritesReady = () => sheet !== null

function frames(name: FoeAnimation): Texture[] {
  return sheet!.animations[name]
}

/**
 * One figure from the archive atlas, standing on (0, 0). The loop starts on a random frame so a pack does not
 * breathe in step. Turn it with the outer `scale.x`, as with the code-drawn looks; the atlas scale stays inside.
 */
export class FoeFigure extends Container {
  private readonly sprite: AnimatedSprite
  private readonly idle: Texture[]
  private readonly walk: Texture[] | null
  private moving = false

  constructor(look: FoeLook) {
    super()
    this.idle = frames(look.idle)
    this.walk = look.walk ? frames(look.walk) : null
    this.sprite = new AnimatedSprite(this.idle)
    this.sprite.updateAnchor = true
    this.sprite.scale.set('scale' in look ? look.scale : look.height / this.idle[0].height)
    this.sprite.animationSpeed = (look.fps ?? DEFAULT_FPS) / TICKS_PER_SECOND
    this.sprite.gotoAndPlay(Math.floor(Math.random() * this.idle.length))
    this.addChild(this.sprite)
  }

  setMoving(moving: boolean) {
    if (!this.walk || moving === this.moving) return
    this.moving = moving
    this.sprite.textures = moving ? this.walk : this.idle
    this.sprite.play()
  }
}

/** A figure of the atlas, or null while the atlas is not loaded. */
export function foeFigure(look: FoeLook): FoeFigure | null {
  return sheet ? new FoeFigure(look) : null
}
