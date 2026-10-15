import { useState } from 'react'
import { api } from '../api'
import { useI18n } from '../i18n'
export function DefensePanel({ slug }: { slug: string }) {
  const { language } = useI18n()
  const ru = language === 'ru'
  const [busy, setBusy] = useState(false), [message, setMessage] = useState('')
  return <section className="card"><h3>{ru ? 'Участок обороны' : 'Defense sector'}</h3>
    <p className="small">{ru ? 'Встань на охрану, затем выбери направление. Перестроение занимает 30 реальных секунд. Назначенная охрана защищает свой подход; «Авто» встречает угрозу у лагеря, но даёт75% личного вклада против направленной атаки.' : 'Start guard duty, then choose a sector. Redeployment takes 30 real seconds. Assigned guards defend their approach; Auto meets threats at camp with75% personal contribution against directional attacks.'}</p>
    <div className="row">{['auto','north','west','south'].map((sector,i)=><button type="button" key={sector} disabled={busy} onClick={async()=>{setBusy(true);try{await api.defenseSector(slug,sector);setMessage(ru?'Приказ принят. Перестроение: 30 реальных секунд.':'Order accepted. Redeployment: 30 real seconds.')}catch{setMessage(ru?'Не удалось. Сначала встань на охрану; раненым это недоступно.':'Could not deploy. Start guard duty; wounded characters cannot deploy.')}finally{setBusy(false)}}}>{(ru?['Авто','Север','Запад','Юг']:['Auto','North','West','South'])[i]}</button>)}</div>
    {message&&<p role="status">{message}</p>}
  </section>
}
