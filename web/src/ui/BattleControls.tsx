import { useState } from 'react'
import { api, type Catalog, type Citizen, type RaidBattle, type RaidUnit, type VillageState } from '../api'
import { useI18n } from '../i18n'

export function BattleControls({ village, battle, unit, citizen, catalog, onClose }: {
  village: VillageState; battle: RaidBattle; unit: RaidUnit; citizen: Citizen | null | undefined;
  catalog: Catalog; onClose: () => void
}) {
  const i18n=useI18n(), ru=i18n.language==='ru'
  const [busy,setBusy]=useState(false),[error,setError]=useState('')
  const fighter=battle.fighters.find(f=>f.citizenId===citizen?.id)
  const guard=!!citizen&&catalog.tasks.some(t=>t.id===citizen.taskId&&t.effect==='Guard')
  const alive=unit.health>0&&!unit.exitedAt&&!battle.finishedAt
  const canOrder=guard&&!!fighter&&fighter.health>0&&fighter.active&&alive
  const raid=village.raids.find(r=>r.id===battle.id)
  const modes=ru?{hold:'Держать позицию',intercept:'Перехватывать'}:{hold:'Hold position',intercept:'Intercept'}
  const phases:Record<string,string>=ru?{Waiting:'Ожидает',Approaching:'Приближается',Fighting:'В бою',Raiding:'Разоряет',Breaching:'Ломает забор',Retreating:'Отступает',Escaped:'Ушёл',Dispersed:'Рассеялся',Dead:'Повержен'}:{}
  const order=async(target:string|null,mode:'hold'|'intercept')=>{
    setBusy(true);setError('')
    try{await api.battleOrder(village.slug,battle.id,target,mode)}catch(e){setError(i18n.error(e))}finally{setBusy(false)}
  }
  return <aside className="battle-controls" aria-label={ru?'Выбранный противник':'Selected enemy'}>
    <header><strong>{raid?i18n.enemy(raid.enemy):ru?'Противник':'Enemy'} · {ru?({weak:'Слабый',regular:'Обычный',strong:'Сильный'}[unit.variant]):unit.variant}</strong><button type="button" className="close" aria-label={i18n.t('close')} onClick={onClose}>✕</button></header>
    <p className="small">{phases[unit.state]??unit.state} · ♥ {Math.ceil(unit.health)}/{Math.ceil(unit.maximumHealth)}</p>
    <progress value={unit.health} max={unit.maximumHealth} aria-label={ru?'Здоровье врага':'Enemy health'}/>
    {fighter&&<p className="small">{ru?'Моё здоровье':'My health'}: {Math.ceil(fighter.health)}/{fighter.maximumHealth} · {ru?'Урон':'Damage'} {Math.round(fighter.damageDealt)}<br/>
      {fighter.weapon?i18n.resource(fighter.weapon):ru?'Без оружия':'Unarmed'} · {fighter.armor?i18n.resource(fighter.armor):ru?'Без брони':'No armor'}{fighter.weapon==='Bow'?` · ➶ ${fighter.arrows??0}`:''}</p>}
    {guard&&!fighter&&<p className="small muted">{ru?'Охранник прибывает на позицию.':'Guard is arriving.'}</p>}
    {!guard&&citizen&&<p className="small muted">{ru?'Для атаки встань на охрану. Текущее дело само не отменяется.':'Start guard duty to attack. Your current work stays active.'}</p>}
    {canOrder&&<div className="battle-orders">
      <button className="primary" disabled={busy} onClick={()=>void order(unit.id,fighter?.mode??'intercept')}>{ru?'Атаковать':'Attack'}</button>
      <button disabled={busy} onClick={()=>void order(null,fighter?.mode??'intercept')}>{ru?'Ближайший враг':'Nearest enemy'}</button>
      {(['hold','intercept'] as const).map(mode=><button key={mode} aria-pressed={fighter?.mode===mode} disabled={busy} onClick={()=>void order(fighter?.targetUnitId??null,mode)}>{modes[mode]}</button>)}
    </div>}
    {error&&<p className="error small" role="status">{error}</p>}
  </aside>
}
