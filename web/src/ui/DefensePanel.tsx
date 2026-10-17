import { useState } from 'react'
import { api, ApiError, type VillageState } from '../api'
import { useI18n } from '../i18n'
export function DefensePanel({ slug, village, ruler=false,tsar=false,onVillage }: { slug:string;village?:VillageState;ruler?:boolean;tsar?:boolean;onVillage?:(v:VillageState)=>void }) {
  const i18n=useI18n(),ru=i18n.language==='ru'
  const hasWall=!!village&&(!!village.fortifications?.length||village.completedProjects.includes('palisade'))
  const gateOpen=village?.fortifications?.length?village.fortifications.some(f=>f.gate&&f.open):true
  const quote=village?.fortificationUpgrade
  const [busy,setBusy]=useState(false),[message,setMessage]=useState('')
  if(!ruler||!hasWall)return null
  const material=(level:number)=>level>=3?(ru?'Каменная стена':'Stone wall'):level===2?(ru?'Бревенчатый частокол':'Log palisade'):(ru?'Деревянный забор':'Wooden fence')
  return <section className="card"><h3>{ru?'Укрепления':'Fortifications'}</h3>
    <p className="small">{ru?'Закрытый вход задерживает врагов. Передвигайся и выбирай цели на карте самостоятельно.':'Closed gates delay enemies. Choose your movement and targets on the map.'}</p>
    <button disabled={busy} onClick={async()=>{setBusy(true);try{await api.gateOrder(slug,!gateOpen);setMessage(ru?'Приказ воротам принят.':'Gate order accepted.')}catch(e){setMessage(i18n.error(e))}finally{setBusy(false)}}}>
      {gateOpen?(ru?'Закрыть ворота':'Close gate'):(ru?'Открыть ворота':'Open gate')}
    </button>
    {quote&&<><p className="small"><strong>{material(quote.level)}</strong> · {ru?'Прочность':'Strength'} ×{quote.strength} · {ru?'Повреждено секций':'Damaged sections'}: {village?.fortifications?.filter(f=>f.damage>0).length??quote.damaged}</p>
      {quote.nextLevel&&<><p className="small">{ru?'Следующий уровень':'Next level'}: {material(quote.nextLevel)}. {i18n.goods(quote.materials)} · {ru?'Строительной работы':'Construction work'}: {quote.work}</p>
        <p className="small muted">{ru?'Повреждения сохранятся. Работа идёт в общей строительной очереди. Если объявят набег, готовое усиление дождётся конца всех волн. Ремонт отдельно: обычная работа и материалы.':'Existing damage remains. Work uses the shared construction queue. If a raid is announced, the completed upgrade waits for all waves to end. Repairs require their own work and materials.'}</p>
        <button disabled={busy||!!quote.blockedBy||!onVillage} onClick={async()=>{setBusy(true);setMessage('');try{onVillage?.(await api.upgradeFortifications(slug));setMessage(ru?(tsar?'Улучшение поставлено в очередь.':'Заявка отправлена царю.'):(tsar?'Upgrade queued.':'Request sent to the Tsar.'))}catch(e){setMessage(i18n.error(e))}finally{setBusy(false)}}}>{tsar?(ru?'Начать улучшение':'Start upgrade'):(ru?'Запросить улучшение':'Request upgrade')}</button>
      </>}
      {quote.readyAfterBattle?<p role="status">{ru?'Материалы и работа готовы. Усиление будет применено после набега; рабочие свободны.':'Materials and work are ready. Reinforcement will apply after the raid; workers are free.'}</p>:quote.blockedBy&&<p className="small muted">{i18n.error(new ApiError(400,quote.blockedBy,quote.blockedBy))}</p>}
    </>}
    {message&&<p role="status">{message}</p>}
  </section>
}
