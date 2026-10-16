import { useRef, useState } from 'react'
import { api, createOperationId, type RaidBattle, type RaidFighter } from '../api'
import { useI18n } from '../i18n'

export function PersonalBattlePanel({slug,battle,fighter,remedies,now,onError}:{slug:string;battle:RaidBattle;fighter:RaidFighter;remedies:number;now:number;onError:(message:string)=>void}) {
  const i18n=useI18n(),ru=i18n.language==='ru'
  const [busy,setBusy]=useState(false),healRequest=useRef<string|null>(null)
  if(battle.finishedAt||battle.phase==='Warning'||!fighter.active||fighter.health<=0||fighter.wounded||fighter.mode==='withdrawn')return null
  const remaining=Math.max(0,Math.ceil((Date.parse(fighter.healReadyAt??'')-now)/1000)||0)
  const retreating=fighter.mode==='retreating'
  const reason=retreating?(ru?'Отходим':'Withdrawing'):remedies<1?(ru?'Нет лекарства':'No remedy'):fighter.health>=fighter.maximumHealth?(ru?'HP полные':'Full health'):remaining>0?`${remaining} ${ru?'с':'s'}`:''
  const act=async(mode:'retreat'|'heal')=>{
    if(busy)return
    setBusy(true)
    if(mode==='heal')healRequest.current??=createOperationId()
    try{await api.battleOrder(slug,battle.id,null,mode,undefined,mode==='heal'?healRequest.current!:undefined);healRequest.current=null}
    catch(e){onError(i18n.error(e))}finally{setBusy(false)}
  }
  return <div className="hud-status personal-battle-panel" role="group" aria-label={ru?'Действия в бою':'Combat actions'}>
    <button disabled={busy||retreating} onClick={()=>void act('retreat')}>{retreating?(ru?'Отступаю…':'Withdrawing…'):(ru?'Отступить':'Retreat')}</button>
    <button disabled={busy||!!reason} onClick={()=>void act('heal')} title={reason||undefined}>{ru?'Лечение':'Heal'}{reason?` · ${reason}`:''}</button>
  </div>
}
