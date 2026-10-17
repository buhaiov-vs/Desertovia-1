import type { ExternalRecovery } from '../api'

export interface RescuePatientView {
  name:string
  injuredUntil?:string|null
  hospitalizedAt?:string|null
  rescueByCitizenId?:string|null
  rescueRequestedAt?:string|null
  recovery?:ExternalRecovery|null
}

export function RescuePatientPanel({patient,now,pending,hasHospital,own,canCarry,carrierName,onClose,onCarry,onAsk,onTreatment}:{
  patient:RescuePatientView;now:number;pending:boolean;hasHospital:boolean;own:boolean;canCarry:boolean;carrierName?:string
  onClose:()=>void;onCarry:()=>void;onAsk:()=>void;onTreatment?:()=>void
}) {
  const external=!!patient.recovery?.until&&Date.parse(patient.recovery.until)>now
  const admitted=external||!!patient.hospitalizedAt
  const until=Math.max(patient.injuredUntil?Date.parse(patient.injuredUntil):0,external?Date.parse(patient.recovery!.until!):0)
  const remaining=Math.max(0,Math.ceil((until-now)/1000))
  const minutes=Math.floor(remaining/60)
  const time=`${minutes} мин ${remaining%60} с`
  const requested=!!patient.rescueRequestedAt&&Date.parse(patient.rescueRequestedAt)>now-60_000
  return <div className="modal-backdrop"><section className="modal" role="dialog" aria-label="Раненый житель">
    <header className="modal-header"><h2>{patient.name}</h2><button aria-label="Закрыть" onClick={onClose}>✕</button></header>
    <div className="modal-body">
      <p>{remaining===0?'Восстановился':external?'На лечении после дуэли':admitted?(hasHospital?'В больнице':'На восстановлении'):patient.rescueByCitizenId?`Помогает: ${carrierName??'житель'}`:'Ранен, ждёт помощи'}</p>
      {remaining>0&&<p>До восстановления: {time}</p>}
      {!external&&!admitted&&remaining>0&&<p className="small muted">На улице идёт самолечение. После доставки в больницу останется четверть срока. Помочь можно в любой момент.</p>}
      {!admitted&&remaining>0&&own&&<button disabled={pending||requested||!!patient.rescueByCitizenId} onClick={onAsk}>{requested?'Помощь запрошена':'Попросить помощи'}</button>}
      {!admitted&&remaining>0&&!own&&canCarry&&<button disabled={pending||!hasHospital||!!patient.rescueByCitizenId} onClick={onCarry}>Отнести в больницу</button>}
      {!admitted&&remaining>0&&!hasHospital&&<p className="small muted">Для доставки нужна действующая больница. Самолечение продолжается.</p>}
      {onTreatment&&<button onClick={onTreatment}>Лечение</button>}
    </div>
  </section></div>
}
