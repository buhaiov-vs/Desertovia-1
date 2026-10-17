import type {Fortification} from '../api'

export type FenceShape='nw'|'h'|'ne'|'north-end'|'v'|'sw'|'se'|'south-end'|'east-end'|'west-end'
export function wallVisualStage(wall:Fortification):number {
  return wall.damage>=100?2:(wall.level??1)>=2&&wall.damage>=50?1:0
}
export function fenceShape(wall:Fortification,walls:readonly Fortification[]):FenceShape {
  if(wall.gate)return 'v'
  const has=(x:number,y:number)=>walls.some(w=>w.x===x&&w.y===y&&!(w.gate&&w.open))
  const left=has(wall.x-1,wall.y),right=has(wall.x+1,wall.y),north=has(wall.x,wall.y-1),south=has(wall.x,wall.y+1)
  if(right&&south)return 'nw'
  if(left&&south)return 'ne'
  if(right&&north)return 'sw'
  if(left&&north)return 'se'
  if(left&&right)return 'h'
  if(left&&!right)return 'east-end'
  if(right&&!left)return 'west-end'
  if(north&&south)return 'v'
  if(south)return 'north-end'
  if(north)return 'south-end'
  return 'h'
}

