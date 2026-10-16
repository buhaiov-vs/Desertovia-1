export interface FighterSample {
  battle:string;revision:number;fromX:number;fromY:number;x:number;y:number;at:number
  cadenceMs?:number;durationMs?:number
}

/** Pace authoritative segments across packet jitter; never extrapolate beyond a received position. */
export function receiveFighterSample(old:FighterSample|undefined,battle:string,revision:number,x:number,y:number,at:number):FighterSample {
  if(old?.battle===battle){
    if(old.revision>=revision)return old
    // A click, hit or health update must not restart a movement or change its cadence.
    if(old.x===x&&old.y===y)return {...old,revision}
  }
  const close=old?.battle===battle&&Math.hypot(old.x-x,old.y-y)<4
  const drawn=close?fighterPositionAt(old,at):{x,y}
  const gap=close?Math.max(0,at-old.at):0
  // Remember recent late packets, then decay gradually. An actual pause/teleport resets pacing.
  const cadenceMs=close&&gap<=3000?Math.min(1750,Math.max(1000,gap,(old.cadenceMs??1000)*.98)):1000
  return {battle,revision,fromX:drawn.x,fromY:drawn.y,x,y,at,cadenceMs,durationMs:cadenceMs+250}
}

export function fighterPositionAt(sample:FighterSample,at:number){
  const t=Math.max(0,Math.min(1,(at-sample.at)/(sample.durationMs??1000)))
  return {x:sample.fromX+(sample.x-sample.fromX)*t,y:sample.fromY+(sample.y-sample.fromY)*t}
}

export function fighterIsMoving(sample:FighterSample,at:number){
  return at-sample.at<(sample.durationMs??1000)&&Math.hypot(sample.x-sample.fromX,sample.y-sample.fromY)>.0001
}
