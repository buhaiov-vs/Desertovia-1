import { useState } from 'react'
import { api, type VillageState } from '../api'
import { useI18n } from '../i18n'
export function DefensePanel({ slug, village, ruler=false }: { slug: string; village?:VillageState; ruler?:boolean }) {
  const { language } = useI18n()
  const ru = language === 'ru'
  const hasWall=!!village&&(!!village.fortifications?.length||village.completedProjects.includes('palisade'))
  const gateOpen=village?.fortifications?.length?village.fortifications.some(f=>f.gate&&f.open):true
  const [busy, setBusy] = useState(false), [message, setMessage] = useState('')
  if(!ruler||!hasWall)return null
  return <section className="card"><h3>{ru?'Ворота':'Gates'}</h3>
    <p className="small">{ru?'Закрытый вход задерживает врагов. Передвигайся и выбирай цели на карте самостоятельно.':'Closed gates delay enemies. Choose your movement and targets on the map.'}</p>
    {ruler&&hasWall&&<button disabled={busy} onClick={async()=>{
      setBusy(true)
      try{await api.gateOrder(slug,!gateOpen);setMessage(ru?'Приказ воротам принят.':'Gate order accepted.')}
      catch{setMessage(ru?'Не удалось изменить ворота.':'Could not change the gate.')}finally{setBusy(false)}
    }}>{gateOpen?(ru?'Закрыть ворота':'Close gate'):(ru?'Открыть ворота':'Open gate')}</button>}
    {message&&<p role="status">{message}</p>}
  </section>
}
