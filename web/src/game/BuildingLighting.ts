import { Container, Graphics, Mesh, MeshGeometry, Shader, type Texture } from 'pixi.js'
import registrations from '../assets/night/registration.json'
import dayAtlas from '../assets/sprites.json'
import outlineStyle from '../../../art/style/building-outline.json'
import { TILE, art, type ArtName, type TileSet } from './sheets'

type Bounds = readonly number[]
export const hasPreparedBuilding = (kind: string) => Object.hasOwn(registrations, kind)

/** Visible walls/roof, not transparent canvas margins, define the registration. */
export function registeredPlacement(bounds: Bounds, width: number, height: number) {
  const scaleX = width / (bounds[2] - bounds[0]), scaleY = height / (bounds[3] - bounds[1])
  return { scaleX, scaleY, x: -bounds[0] * scaleX, y: -bounds[1] * scaleY }
}

const vertex = `
in vec2 aPosition;
uniform mat3 uProjectionMatrix;
uniform mat3 uWorldTransformMatrix;
uniform mat3 uTransformMatrix;
out vec2 vPosition;
void main() {
  vPosition = aPosition;
  gl_Position = vec4((uProjectionMatrix * uWorldTransformMatrix * uTransformMatrix * vec3(aPosition, 1.0)).xy, 0.0, 1.0);
}`
const fragment = `
in vec2 vPosition;
uniform sampler2D uDayTexture;
uniform sampler2D uNightTexture;
uniform vec4 uDayMap;
uniform vec4 uNightMap;
uniform vec4 uDayFrame;
uniform vec4 uNightFrame;
uniform float uNight;
uniform float uEmission;
uniform vec4 uColor;
out vec4 finalColor;
vec4 dayPixel(vec2 uv) {
  if (any(lessThan(uv, uDayFrame.xy)) || any(greaterThan(uv, uDayFrame.zw))) return vec4(0.0);
  return texture(uDayTexture, uv);
}
vec4 nightPixel(vec2 uv) {
  if (any(lessThan(uv, uNightFrame.xy)) || any(greaterThan(uv, uNightFrame.zw))) return vec4(0.0);
  return texture(uNightTexture, uv);
}
// A shared world-space contour: PNG resolution and registered scaling cannot change its thickness.
vec2 bodyAt(vec2 position) {
  float day = dayPixel(position * uDayMap.xy + uDayMap.zw).a;
  float night = nightPixel(position * uNightMap.xy + uNightMap.zw).a;
  return vec2(step(${outlineStyle.alphaThreshold / 255}, day), step(${outlineStyle.alphaThreshold / 255}, night));
}
void main() {
  vec4 day = dayPixel(vPosition * uDayMap.xy + uDayMap.zw);
  vec4 night = nightPixel(vPosition * uNightMap.xy + uNightMap.zw);
  if (uEmission > 0.5) {
    // Only warm lamps/windows/fire emit. Cold moonlit roofs remain dark.
    float warm = smoothstep(0.30, 0.65, night.r) * smoothstep(0.10, 0.45, night.r - night.b);
    finalColor = night * vec4(1.10, 0.80, 0.40, 1.0) * warm * uColor;
    return;
  }
  // Compensate for the existing blue map overlay, so it does not extinguish the windows.
  float shade = uNight * 0.55;
  night.rgb = clamp((night.rgb - night.a * vec3(11.0,22.0,64.0)/255.0 * shade)/(1.0-shade), vec3(0.0), vec3(night.a));
  // One premultiplied interpolation: unlike two fading sprites, opaque walls never become transparent at dusk.
  vec2 body = vec2(step(${outlineStyle.alphaThreshold / 255}, day.a), step(${outlineStyle.alphaThreshold / 255}, night.a));
  if (mix(body.x, body.y, uNight) >= 1.0) { finalColor = mix(day, night, uNight) * uColor; return; }
  vec2 nearby = vec2(0.0);
  for (int x = -1; x <= 1; x++) {
    for (int y = -1; y <= 1; y++) {
      if (x == 0 && y == 0) continue;
      nearby = max(nearby, bodyAt(vPosition + vec2(float(x), float(y)) * ${outlineStyle.worldWidth.toFixed(1)}));
    }
  }
  vec2 edge = nearby * (vec2(1.0) - body);
  // The glow-only pass returned above: light halos don't acquire a dark contour.
  vec3 ink = vec3(${[1, 3, 5].map(offset => (parseInt(outlineStyle.color.slice(offset, offset + 2), 16) / 255).toFixed(6)).join(', ')});
  // Outline each endpoint before crossfading: overlapping opaque walls/ink stay opaque at dusk.
  finalColor = mix(mix(day, vec4(ink, 1.0), edge.x), mix(night, vec4(ink, 1.0), edge.y), uNight) * uColor;
}`

function sampling(texture: Texture, p: ReturnType<typeof registeredPlacement>) {
  const { width, height } = texture.source, f = texture.frame
  return {
    map: [1/(p.scaleX*width),1/(p.scaleY*height),f.x/width-p.x/(p.scaleX*width),f.y/height-p.y/(p.scaleY*height)],
    frame: [(f.x+0.5)/width,(f.y+0.5)/height,(f.x+f.width-0.5)/width,(f.y+f.height-0.5)/height],
    bounds: [p.x,p.y,p.x+f.width*p.scaleX,p.y+f.height*p.scaleY],
  }
}

/** A fixed, aligned mesh with two preloaded textures; state updates retain the same light level. */
export class BuildingLighting extends Container {
  private readonly mesh: Mesh<MeshGeometry, Shader>
  private readonly shader: Shader
  private readonly glowShader: Shader
  /** Emission is rendered above map darkness, like the existing campfire light. */
  readonly light = new Container()
  private readonly emitsLight: boolean
  constructor(tiles: TileSet, kind: string, x: number, y: number, plotWidth = 3, plotHeight = 3, emitsLight = true) {
    super()
    this.emitsLight = emitsLight
    const pair = registrations[kind as keyof typeof registrations]
    const [left, top, right, bottom] = pair.dayBounds
    const scale = Math.min(0.5, plotWidth*TILE/(right-left),plotHeight*TILE/(bottom-top))
    const width = (right-left)*scale, height = (bottom-top)*scale
    const dayTexture = tiles.get(art(pair.day as ArtName)), nightTexture = tiles.nightBuilding(pair.night)
    const day = sampling(dayTexture,registeredPlacement(pair.dayBounds.map(value=>value/dayAtlas.meta.scale),width,height))
    const night = sampling(nightTexture,registeredPlacement(pair.nightBounds,width,height))
    const border = outlineStyle.worldWidth
    const x0=Math.min(day.bounds[0],night.bounds[0])-border,y0=Math.min(day.bounds[1],night.bounds[1])-border
    const x1=Math.max(day.bounds[2],night.bounds[2])+border,y1=Math.max(day.bounds[3],night.bounds[3])+border
    const geometry = new MeshGeometry({ positions: new Float32Array([x0,y0,x1,y0,x1,y1,x0,y1]),
      uvs:new Float32Array([0,0,1,0,1,1,0,1]),indices:new Uint32Array([0,1,2,0,2,3]) })
    geometry.batchMode='no-batch'
    this.shader=Shader.from({gl:{vertex,fragment},resources:{
      uDayTexture:dayTexture.source,uNightTexture:nightTexture.source,
      lighting:{uDayMap:{value:day.map,type:'vec4<f32>'},uNightMap:{value:night.map,type:'vec4<f32>'},
        uDayFrame:{value:day.frame,type:'vec4<f32>'},uNightFrame:{value:night.frame,type:'vec4<f32>'},uNight:{value:0,type:'f32'},uEmission:{value:0,type:'f32'}},
    }})
    this.mesh=new Mesh({geometry,shader:this.shader,texture:dayTexture})
    this.glowShader = Shader.from({gl:{vertex,fragment},resources:{
      uDayTexture:dayTexture.source,uNightTexture:nightTexture.source,
      lighting:{uDayMap:{value:day.map,type:'vec4<f32>'},uNightMap:{value:night.map,type:'vec4<f32>'},
        uDayFrame:{value:day.frame,type:'vec4<f32>'},uNightFrame:{value:night.frame,type:'vec4<f32>'},
        uNight:{value:0,type:'f32'},uEmission:{value:1,type:'f32'}},
    }})
    const emission = new Mesh({geometry,shader:this.glowShader,texture:nightTexture})
    emission.blendMode = 'add'
    const pool = new Graphics()
    for (const [radius,opacity] of [[2,0.04],[1.5,0.06],[1,0.10],[0.6,0.18]]) {
      pool.ellipse(width/2,height,radius*TILE,radius*TILE*0.65).fill({color:0xffa145,alpha:opacity})
    }
    pool.blendMode = 'add'
    this.light.addChild(pool,emission)
    this.position.set((x+plotWidth/2)*TILE-width/2,(y+plotHeight)*TILE-height)
    this.zIndex=(y+plotHeight)*TILE
    this.addChild(this.mesh)
    this.light.position.copyFrom(this.position)
    this.light.alpha = 0
  }
  setNight(amount: number) {
    const level = Math.max(0,Math.min(1,amount))
    this.shader.resources.lighting.uniforms.uNight=level
    this.light.alpha=this.emitsLight ? level : 0
  }
  override destroy(options?: Parameters<Container['destroy']>[0]) {
    const geometry = this.mesh.geometry
    this.light.destroy({children:true})
    this.glowShader.destroy()
    super.destroy(options)
    this.shader.destroy()
    geometry.destroy()
  }
}
