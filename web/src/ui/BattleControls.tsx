import { useEffect, useState } from 'react'
import { api, type Catalog, type Citizen, type RaidBattle, type RaidUnit, type VillageState } from '../api'
import { useI18n } from '../i18n'

export function BattleControls({ village, battle, unit, citizen, catalog, tsar=false, accountId, onDefend, onClose }: {
  village: VillageState; battle: RaidBattle; unit: RaidUnit; citizen: Citizen | null | undefined;
  catalog: Catalog; tsar?:boolean; accountId?:string; onDefend?:()=>void; onClose: () => void
}) {
  const i18n=useI18n(), ru=i18n.language==='ru'
  const [busy,setBusy]=useState(false),[error,setError]=useState('')
  const [now,setNow]=useState(()=>Date.now())
  useEffect(()=>{const timer=window.setInterval(()=>setNow(Date.now()),1000);return()=>window.clearInterval(timer)},[])
  const fighter=battle.fighters.find(f=>tsar?f.isTsar&&f.accountId===accountId:!f.isTsar&&f.citizenId===citizen?.id)
  const guard=!!citizen&&catalog.tasks.some(t=>t.id===citizen.taskId&&t.effect==='Guard')
  const alive=unit.health>0&&!unit.exitedAt&&!battle.finishedAt
  const recovering=tsar&&!!village.tsar.injuredUntil&&Date.parse(village.tsar.injuredUntil)>now
  const canOrder=(tsar||guard)&&alive&&!recovering&&(!fighter||fighter.health>0)
  const raid=village.raids.find(r=>r.id===battle.id)
  const modes=ru?{hold:'Держать позицию',intercept:'Перехватывать'}:{hold:'Hold position',intercept:'Intercept'}
  const phases:Record<string,string>=ru?{Waiting:'Ожидает',Approaching:'Приближается',Fighting:'В бою',Raiding:'Разоряет',Breaching:'Ломает забор',Retreating:'Отступает',Escaped:'Ушёл',Dispersed:'Рассеялся',Dead:'Повержен'}:{}
  const order=async(target:string|null,mode:'hold'|'intercept')=>{
    if(busy)return;setBusy(true);setError('')
    try{await api.battleOrder(village.slug,battle.id,target,mode)}catch(e){setError(i18n.error(e))}finally{setBusy(false)}
  }
  return <aside className="battle-controls" aria-label={ru?'Выбранный противник':'Selected enemy'}>
    <header><strong>{raid?i18n.enemy(raid.enemy):ru?'Противник':'Enemy'} · {ru?({weak:'Слабый',regular:'Обычный',strong:'Сильный'}[unit.variant]):unit.variant}</strong><button type="button" className="close" aria-label={i18n.t('close')} onClick={e=>{e.stopPropagation();onClose()}}><svg viewBox="0 0 24 24" aria-hidden="true"><path d="m6 6 12 12M18 6 6 18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"/></svg></button></header>
    <p className="small">{phases[unit.state]??unit.state} · ♥ {Math.ceil(unit.health)}/{Math.ceil(unit.maximumHealth)}</p>
    <progress value={unit.health} max={unit.maximumHealth} aria-label={ru?'Здоровье врага':'Enemy health'}/>
    {fighter&&<p className="small">{ru?'Моё здоровье':'My health'}: {Math.ceil(fighter.health)}/{fighter.maximumHealth} · {ru?'Урон':'Damage'} {Math.round(fighter.damageDealt)}<br/>
      {fighter.isTsar?(ru?'Воевода':'Voivode'):fighter.weapon?i18n.resource(fighter.weapon):ru?'Без оружия':'Unarmed'}{fighter.arrows?` · ➶ ${fighter.arrows}`:''}</p>}
    {recovering&&<p role="status">{ru?'Царь на лечении до':'The Tsar is recovering until'} {new Date(village.tsar.injuredUntil!).toLocaleTimeString(i18n.language,{hour:'2-digit',minute:'2-digit'})}</p>}
    {!canOrder&&fighter&&fighter.health<=0&&<p>{ru?'Вы ранены. Дождитесь восстановления.':'You are wounded. Wait for recovery.'}</p>}
    {!guard&&!tsar&&citizen&&<><p className="small muted">{ru?'Для атаки встань на защиту. Текущее дело само не отменяется.':'Join the defense to attack. Your current work stays active.'}</p>{onDefend&&<button disabled={busy||citizen.state==='Working'||citizen.state==='Paused'} onClick={onDefend}>{ru?'Встать на защиту':'Join defense'}</button>}</>}
    {canOrder&&<div className="battle-orders">
      <button className="primary" disabled={busy} onClick={()=>void order(unit.id,fighter?.mode??'intercept')}>{tsar&&!fighter?(ru?'Вступить в бой и атаковать':'Join battle and attack'):ru?'Атаковать':'Attack'}</button>
      <button disabled={busy} onClick={()=>void order(null,fighter?.mode??'intercept')}>{ru?'Ближайший враг':'Nearest enemy'}</button>
      {(['hold','intercept'] as const).map(mode=><button key={mode} aria-pressed={fighter?.mode===mode} disabled={busy} onClick={()=>void order(fighter?.targetUnitId??null,mode)}>{modes[mode]}</button>)}
    </div>}
    {error&&<p className="error small" role="status">{error}</p>}
  </aside>
}
