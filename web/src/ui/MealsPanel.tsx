import {useRef,useState} from 'react'
import {api,type Citizen} from '../api'
import {useI18n} from '../i18n'
import {ResourceIcon} from './TileIcon'

export function MealsPanel({slug,citizen,now,pending,onCitizen}:{slug:string;citizen:Citizen;now:number;pending:boolean;onCitizen?:(c:Citizen)=>void}){
 const i18n=useI18n(),ru=i18n.language==='ru',lock=useRef(false)
 const [busy,setBusy]=useState(false),[error,setError]=useState('')
 const active=!!citizen.mealUntil&&Date.parse(citizen.mealUntil)>now
 const cooling=!!citizen.mealReadyAt&&Date.parse(citizen.mealReadyAt)>now
 const unavailable=citizen.status==='Dormant'||citizen.state==='Paused'||!!citizen.exhaustedUntil&&Date.parse(citizen.exhaustedUntil)>now||!!citizen.injuredUntil&&Date.parse(citizen.injuredUntil)>now||!!citizen.recovery&&Date.parse(citizen.recovery.until??'')>now
 return <section className="card"><h3>{ru?'Блюда':'Prepared meals'}</h3>
  <p className="small muted">{ru?'Каша: расход сил на работу −10%. Похлёбка: урон в набеге +5%. Эффект 20 минут, перерыв 30 минут; одновременно одно блюдо. HP и силы не восстанавливаются. Готовь в пекарне или корчме, затем возьми порцию со склада.':'Porridge: work energy cost −10%. Stew: raid damage +5%. One effect for 20 minutes; 30 minute cooldown. No HP or energy restored. Cook at the bakery or tavern and take a portion from storage.'}</p>
  {active&&<p role="status">{i18n.resource(citizen.mealKind??'Food')} · {Math.ceil((Date.parse(citizen.mealUntil!)-now)/60000)} {ru?'мин':'min'}</p>}
  <div className="row">{['Porridge','Stew'].map(resource=>{
   const amount=(citizen.load.find(g=>g.resource===resource)?.amount??0)+(citizen.personalGoods?.find(g=>g.kind===resource)?.amount??0)
   return <button key={resource} disabled={pending||busy||active||cooling||unavailable||amount<1||!onCitizen} onClick={async()=>{
    if(lock.current)return;lock.current=true;setBusy(true);setError('')
    try{onCitizen?.(await api.eatMeal(slug,resource))}catch(e){setError(i18n.error(e))}finally{setBusy(false);lock.current=false}
   }}><ResourceIcon kind={resource} scale={1.5}/> {i18n.resource(resource)} · {amount}</button>
  })}</div>
  {cooling&&!active&&<p className="small muted">{ru?'Следующее блюдо через':'Next meal in'} {Math.ceil((Date.parse(citizen.mealReadyAt!)-now)/60000)} {ru?'мин':'min'}</p>}
  {error&&<p className="error" role="alert">{error}</p>}
 </section>
}
