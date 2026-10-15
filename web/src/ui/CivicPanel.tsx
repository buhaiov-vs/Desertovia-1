import { useState } from 'react'
import { api, type VillageState, type Citizen } from '../api'

/** Shared operations card: commands remain server-authorized; governors only prepare requests. */
export function CivicPanel({village,tsar,ruler,actorId,citizen,onVillage,plotId}:{village:VillageState;tsar:boolean;ruler:boolean;actorId:string;citizen?:Citizen|null;onVillage:(v:VillageState)=>void;plotId?:string}) {
  const [busy,setBusy]=useState(false),[error,setError]=useState(''),[confirm,setConfirm]=useState<string|null>(null)
  const run=async(action:()=>Promise<VillageState>)=>{if(busy)return;setBusy(true);setError('');try{onVillage(await action());setConfirm(null)}catch(e){setError(e instanceof Error?e.message:'Не удалось выполнить действие')}finally{setBusy(false)}}
  const privateBuilding = [...(village.structures??[]),...(village.storages??[])].find(b=>b.plotId===plotId)?.ownerAccountId
  const homes=(village.housing??[]).filter(h=>(!plotId||h.plotId===plotId)&&!(village.structures??[]).some(s=>s.plotId===h.plotId&&s.ownerAccountId&&s.ownerAccountId!==actorId))
  const orders=(village.civicOrders??[]).filter(o=>o.status==='pending' && (tsar||ruler||o.requestedBy===actorId||o.partnerAccountId===actorId))
  return <section className="civic-panel">
    {error&&<p className="error" role="alert">{error}</p>}
    {privateBuilding&&<p>Личная постройка</p>}
    {plotId&&ruler&&!privateBuilding&&<div className="civic-actions"><button disabled={busy} onClick={()=>run(()=>api.civic(village.slug,plotId,'upgrade'))}>Улучшить{tsar?'':' · заявка'}</button>
      {confirm===plotId?<><span>Разобрать медленно? Возврат: 25% учтённых материалов.</span><button disabled={busy} onClick={()=>run(()=>api.civic(village.slug,plotId,'demolish'))}>Подтвердить снос</button><button onClick={()=>setConfirm(null)}>Отмена</button></>:<button disabled={busy} onClick={()=>setConfirm(plotId)}>Снести{tsar?'':' · заявка'}</button>}</div>}
    {!plotId&&<h3>Жильё и заявки</h3>}
    {citizen?.spouseName&&<p>Брак: {citizen.spouseName}</p>}
    {homes.map(h=><div className="civic-home" key={h.plotId}><span>Жильё · уровень {h.level} · {h.occupied}/{h.beds}</span>
      {citizen&&citizen.homePlotId!==h.plotId&&<button disabled={busy||h.occupied>=h.beds} onClick={()=>run(()=>api.civic(village.slug,h.plotId,'housing'))}>Попросить место</button>}
      {citizen?.spouseAccountId&&<button disabled={busy||h.beds-h.occupied<2} onClick={()=>run(()=>api.civic(village.slug,h.plotId,'housing',citizen.spouseAccountId!))}>Заявка для супругов</button>}
    </div>)}
    {!plotId&&orders.map(o=><article className="civic-order" key={o.id}><span>{o.operation==='housing'?'Заселение':o.operation==='upgrade'?'Улучшение':'Разборка'} · участок {o.plotId}</span>
      {o.partnerAccountId&&!o.partnerAccepted&&<small>Ожидаем согласие супруга</small>}
      {(tsar||o.partnerAccountId===actorId&&!o.partnerAccepted)&&<div><button disabled={busy} onClick={()=>run(()=>api.civicDecision(village.slug,o.id,true))}>{tsar?'Одобрить':'Согласиться'}</button><button disabled={busy} onClick={()=>run(()=>api.civicDecision(village.slug,o.id,false))}>Отклонить</button></div>}
    </article>)}
  </section>
}
