import { useState } from 'react'
import { useI18n } from '../i18n'
import { api, type VillageState, type Citizen } from '../api'

/** Shared operations card: commands remain server-authorized; governors only prepare requests. */
export function CivicPanel({village,tsar,ruler,actorId,citizen,onVillage,plotId}:{village:VillageState;tsar:boolean;ruler:boolean;actorId:string;citizen?:Citizen|null;onVillage:(v:VillageState)=>void;plotId?:string}) {
  const i18n=useI18n(), ru=i18n.language==='ru'
  const [busy,setBusy]=useState(false),[error,setError]=useState(''),[confirm,setConfirm]=useState<string|null>(null)
  const run=async(action:()=>Promise<VillageState>)=>{if(busy)return;setBusy(true);setError('');try{onVillage(await action());setConfirm(null)}catch(e){setError(i18n.error(e))}finally{setBusy(false)}}
  const privateBuilding = [...(village.structures??[]),...(village.storages??[])].find(b=>b.plotId===plotId)?.ownerAccountId
  const selectedBuilding=village.structures?.find(s=>s.plotId===plotId)
  const selectedStorage=village.storages?.find(s=>s.plotId===plotId)
  const canUpgrade=!!(selectedBuilding&&['Castle','Longhouse'].includes(selectedBuilding.kind)&&(selectedBuilding.level??1)<3) || !!(selectedStorage&&['WoodPile','StonePile','DryingRack'].includes(selectedStorage.kind))
  const hospitalUpgrade=selectedBuilding?.kind==='Hospital'&&(selectedBuilding.level??1)<3
  const upgradeAtMaximum=!!(selectedBuilding&&['Castle','Longhouse','Hospital'].includes(selectedBuilding.kind)&&(selectedBuilding.level??1)>=3) || !!(selectedStorage&&['Woodyard','StoneYard','Granary'].includes(selectedStorage.kind))
  const homes=(village.housing??[]).filter(h=>(!plotId||h.plotId===plotId)&&!(village.structures??[]).some(s=>s.plotId===h.plotId&&s.ownerAccountId&&s.ownerAccountId!==actorId))
  const orders=(village.civicOrders??[]).filter(o=>o.status==='pending' && (tsar||ruler||o.requestedBy===actorId||o.partnerAccountId===actorId))
  const buildingName=(plot:string)=>{
    const building=village.structures?.find(s=>s.plotId===plot)
    const storage=village.storages?.find(s=>s.plotId===plot)
    return building?i18n.building(building.kind).name:storage?i18n.storage(storage.kind):(ru?'Дом':'Home')
  }
  const free=homes.reduce((n,h)=>n+h.beds-h.occupied,0)
  const orderLabel=(operation:string)=>operation==='build'?(ru?'Строительство':'Construction'):operation==='housing'?(ru?'Заселение':'Housing'):operation==='upgrade'?(ru?'Улучшение здания':'Building upgrade'):(ru?'Разборка':'Dismantle')
  const homeCards=homes.map(h=><div className="civic-home" key={h.plotId}>
    <strong>{buildingName(h.plotId)} · {h.plotId}</strong>
    <span>{ru?'Уровень':'Level'} {h.level} · {ru?'Занято мест':'Occupied beds'}: {h.occupied}/{h.beds}{citizen?.homePlotId===h.plotId?(ru?' · Мой дом':' · My home'):''}</span>
    {citizen&&citizen.homePlotId!==h.plotId&&<button disabled={busy||h.occupied>=h.beds||orders.some(o=>o.operation==='housing'&&o.requestedBy===actorId)} onClick={()=>run(()=>api.civic(village.slug,h.plotId,'housing'))}>{ru?'Попросить место':'Request a bed'}</button>}
    {citizen?.spouseAccountId&&<button disabled={busy||h.beds-h.occupied<2||orders.some(o=>o.operation==='housing'&&o.requestedBy===actorId)} onClick={()=>run(()=>api.civic(village.slug,h.plotId,'housing',citizen.spouseAccountId!))}>{ru?'Заявка для супругов':'Request for spouses'}</button>}
  </div>)
  return <section className="civic-panel">
    {error&&<p className="error" role="alert">{error}</p>}
    {privateBuilding&&<p>Личная постройка</p>}
    {plotId&&ruler&&!privateBuilding&&<div className="civic-actions"><button disabled={busy||!canUpgrade} onClick={()=>run(()=>api.civic(village.slug,plotId,'upgrade'))}>{canUpgrade?(ru?(tsar?'Улучшить':'Улучшить · заявка'):(tsar?'Upgrade':'Request upgrade')):hospitalUpgrade?(ru?'В карточке больницы':'In the hospital card'):upgradeAtMaximum?(ru?'Максимальный уровень':'Maximum level'):(ru?'Нет улучшений':'No upgrades')}</button>
      {confirm===plotId?<><span>Разобрать медленно? Возврат: 25% учтённых материалов.</span><button disabled={busy} onClick={()=>run(()=>api.civic(village.slug,plotId,'demolish'))}>Подтвердить снос</button><button onClick={()=>setConfirm(null)}>Отмена</button></>:<button disabled={busy} onClick={()=>setConfirm(plotId)}>Снести{tsar?'':' · заявка'}</button>}</div>}
    {plotId&&ruler&&!privateBuilding&&(selectedBuilding||selectedStorage)&&!canUpgrade&&<p className="small muted">{ru?(hospitalUpgrade?'Царь может улучшить больницу в её карточке.':upgradeAtMaximum?'Достигнут максимальный уровень улучшения.':'Для этого типа здания улучшение не предусмотрено.'):(hospitalUpgrade?'The Tsar can upgrade the hospital from its hospital card.':upgradeAtMaximum?'The building has reached its maximum upgrade level.':'No upgrade is available for this building type.')}</p>}
    {citizen?.spouseName&&<p>{ru?'Брак':'Spouse'}: {citizen.spouseName}</p>}
    {plotId?homeCards:<>
      <details className="civic-housing"><summary>{ru?'Жильё':'Housing'} · {homes.length} · {ru?'Свободно мест':'Free beds'}: {free}</summary>
        <p className="small muted">{ru?'Это построенные дома, а не заявки. Заселение одобряет царь.':'These are completed homes, not applications. The Tsar approves housing.'}</p>
        {homeCards}
      </details>
      <details className="civic-applications"><summary>{ru?'Заявки на рассмотрении':'Pending applications'} · {orders.length}</summary>
        {orders.length===0&&<p className="small muted">{ru?'Новых заявок нет':'No pending applications'}</p>}
        {orders.map(o=><article className="civic-order" key={o.id}>
          <strong>{orderLabel(o.operation)} · {o.operation==='build'&&o.buildingKind?i18n.building(o.buildingKind).name:buildingName(o.plotId)}</strong><small>{ru?'Координаты':'Coordinates'}: {o.plotId}</small>
          {o.operation==='build'&&<span className="small">{ru?'Заявка посадника. Стройка начнётся только после одобрения царём.':'Governor application. Construction starts only after the Tsar approves.'}</span>}
          {o.operation==='upgrade'&&<span className="small">{ru?'Начать отдельную стройку улучшения. Материалы и работа потребуются после одобрения.':'Start an upgrade construction. Materials and work are required after approval.'}</span>}
          {o.partnerAccountId&&!o.partnerAccepted&&<small>{ru?'Ожидаем согласие супруга':'Waiting for spouse consent'}</small>}
          {(tsar||o.partnerAccountId===actorId&&!o.partnerAccepted)&&<div className="row"><button disabled={busy} onClick={()=>run(()=>api.civicDecision(village.slug,o.id,true))}>{tsar?(ru?'Одобрить':'Approve'):(ru?'Согласиться':'Accept')}</button><button disabled={busy} onClick={()=>run(()=>api.civicDecision(village.slug,o.id,false))}>{ru?'Отклонить':'Reject'}</button></div>}
        </article>)}
      </details>
    </>}
  </section>
}
