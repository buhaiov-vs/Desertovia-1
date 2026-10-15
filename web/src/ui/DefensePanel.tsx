import { useState } from 'react'
import { api, type VillageState } from '../api'
import { useI18n } from '../i18n'
export function DefensePanel({ slug, village, ruler=false,canDeploy=true }: { slug: string; village?:VillageState; ruler?:boolean;canDeploy?:boolean }) {
  const { language } = useI18n()
  const ru = language === 'ru'
  const hasWall=!!village&&(!!village.fortifications?.length||village.completedProjects.includes('palisade'))
  const gateOpen=village?.fortifications?.length?village.fortifications.some(f=>f.gate&&f.open):true
  const [busy, setBusy] = useState(false), [message, setMessage] = useState('')
  return <section className="card"><h3>{ru ? 'Участок обороны' : 'Defense sector'}</h3>
    <p className="small">{!canDeploy?(ru?'Воротами управляют царь и посадник. Закрытый вход задерживает врагов, повреждённый забор можно ремонтировать.':'Rulers control the gate. Closed gates delay raiders; damaged palisades can be repaired.'):ru ? 'Встань на охрану, затем выбери направление. Перестроение занимает 30 реальных секунд. Охранник защищает выбранный подход, «Авто» — центр. В бою выбери врага на карте; удары идут автоматически с обычной скоростью.' : 'Start guard duty, then choose a sector. Redeployment takes 30 real seconds. Guards defend their assigned approach; Auto holds the centre. Select a map enemy; attacks follow the server cooldown.'}</p>
    {canDeploy&&<div className="row">{['auto','north','west','south'].map((sector,i)=><button type="button" key={sector} disabled={busy} onClick={async()=>{setBusy(true);try{await api.defenseSector(slug,sector);setMessage(ru?'Приказ принят. Перестроение: 30 реальных секунд.':'Order accepted. Redeployment: 30 real seconds.')}catch{setMessage(ru?'Не удалось. Сначала встань на охрану; раненым это недоступно.':'Could not deploy. Start guard duty; wounded characters cannot deploy.')}finally{setBusy(false)}}}>{(ru?['Авто','Север','Запад','Юг']:['Auto','North','West','South'])[i]}</button>)}</div>}
    {ruler&&hasWall&&<button disabled={busy} onClick={async()=>{
      setBusy(true)
      try{await api.gateOrder(slug,!gateOpen);setMessage(ru?'Приказ воротам принят.':'Gate order accepted.')}
      catch{setMessage(ru?'Не удалось изменить ворота.':'Could not change the gate.')}finally{setBusy(false)}
    }}>{gateOpen?(ru?'Закрыть ворота':'Close gate'):(ru?'Открыть ворота':'Open gate')}</button>}
    {message&&<p role="status">{message}</p>}
  </section>
}
