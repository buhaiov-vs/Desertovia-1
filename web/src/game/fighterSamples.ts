export interface FighterSample {
  battle:string;revision:number;fromX:number;fromY:number;x:number;y:number;at:number
}

/** Status-only revisions must not jump to a position that is still being interpolated. */
export function receiveFighterSample(old:FighterSample|undefined,battle:string,revision:number,x:number,y:number,at:number):FighterSample {
  if(old?.battle===battle){
    if(old.revision>=revision)return old
    if(old.x===x&&old.y===y)return {...old,revision}
  }
  const close=old?.battle===battle&&Math.hypot(old.x-x,old.y-y)<4
  return {battle,revision,fromX:close?old.x:x,fromY:close?old.y:y,x,y,at}
}

export function fighterPositionAt(sample:FighterSample,at:number){
  const t=Math.max(0,Math.min(1,(at-sample.at)/1000))
  return {x:sample.fromX+(sample.x-sample.fromX)*t,y:sample.fromY+(sample.y-sample.fromY)*t}
}
