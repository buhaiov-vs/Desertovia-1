import { Container, Graphics, Sprite } from 'pixi.js'
import type { Site } from '../api'
import { BuildingLighting } from './BuildingLighting'
import { TILE, town, type TileSet } from './sheets'

/** Fixed footprint, construction stages and materials outside the walls. */
export class HospitalConstruction extends Container {
  readonly roofAnchor: { x: number; y: number }
  readonly light = new Container()
  private readonly building?: BuildingLighting
  constructor(tiles: TileSet, site: Site, x: number, y: number) {
    super()
    this.position.set(x * TILE, y * TILE)
    this.zIndex = (y + 3) * TILE
    this.roofAnchor = { x: x * TILE + 24, y: y * TILE + (site.stage === 0 ? 42 : site.stage === 1 ? 29 : site.stage === 2 ? 7 : site.stage === 3 ? 18 : 0) }
    if ((site.targetLevel ?? 1) === 1) {
      this.addChild(new Graphics().roundRect(2, 2, 44, 44, 3).fill({ color: 0x715c43, alpha: .4 }))
      if (site.stage === 1) this.addChild(new Graphics().rect(5, 29, 38, 14).stroke({ color: 0x797c79, width: 3 }))
      if (site.stage === 2) {
        const frame = new Graphics()
        for (const px of [7, 24, 41]) frame.moveTo(px, 43).lineTo(px, 17).stroke({ color: 0x754c2d, width: 2 })
        frame.moveTo(7, 17).lineTo(24, 7).lineTo(41, 17).stroke({ color: 0x754c2d, width: 2 })
        this.addChild(frame)
      }
      if (site.stage >= 3) {
        const built = new BuildingLighting(tiles, 'Hospital', 0, 0, 3, 3, false)
        const reveal = site.stage >= 4 ? 1 : .63
        const mask = new Graphics().rect(-3, 48 * (1 - reveal), 54, 48 * reveal + 3).fill(0xffffff)
        built.mask = mask
        this.addChild(built, mask)
        this.building = built
      }
    }
    for (const [px, py] of [[0, 0], [44, 0], [0, 44], [44, 44]])
      this.addChild(new Graphics().rect(px, py, 2, 6).fill(0x67462e))
    if (site.stages[site.stage]?.jobs.some(j => j.done > 0 && j.jobId.startsWith('bring-')))
      for (let n = 0; n < 3; n++) {
        const log = new Sprite(tiles.get(town(106))); log.scale.set(.5); log.position.set(49, 34 - n * 4); this.addChild(log)
      }
  }
  setNight(amount: number) { this.building?.setNight(amount) }
  override destroy(options?: Parameters<Container['destroy']>[0]) {
    this.light.destroy({ children: true })
    super.destroy(options)
  }
}
