import { useState } from 'react'
import { api, ApiError, siteHasWork, type VillageState } from '../api'
import { useI18n } from '../i18n'
import { SiteActions } from './SiteActions'
export function DefensePanel({ slug, village, ruler=false,tsar=false,onVillage }: { slug:string;village?:VillageState;ruler?:boolean;tsar?:boolean;onVillage?:(v:VillageState)=>void }) {
  const i18n=useI18n(),ru=i18n.language==='ru'
  const hasWall=!!village&&(!!village.fortifications?.length||village.completedProjects.includes('palisade'))
  const gateOpen=village?.fortifications?.length?village.fortifications.some(f=>f.gate&&f.open):true
  const quote=village?.fortificationUpgrade
  const site=village?.sites.find(s=>s.plotId==='fortifications')
  const [busy,setBusy]=useState(false),[message,setMessage]=useState('')
  if(!ruler||!hasWall)return null
  const material=(level:number)=>level>=3?(ru?'Каменная стена':'Stone wall'):level===2?(ru?'Бревенчатый частокол':'Log palisade'):(ru?'Деревянный забор':'Wooden fence')
  return <section className="card" aria-label={ru?'Забор и ворота':'Fence and gates'}><h3>{ru?'Забор и ворота':'Fence and gates'}</h3>
    <p className="small">{ru?'Закрытый вход задерживает врагов. Передвигайся и выбирай цели на карте самостоятельно.':'Closed gates delay enemies. Choose your movement and targets on the map.'}</p>
    <button disabled={busy} onClick={async()=>{setBusy(true);try{await api.gateOrder(slug,!gateOpen);setMessage(ru?'Приказ воротам принят.':'Gate order accepted.')}catch(e){setMessage(i18n.error(e))}finally{setBusy(false)}}}>
      {gateOpen?(ru?'Закрыть ворота':'Close gate'):(ru?'Открыть ворота':'Open gate')}
    </button>
    {quote&&<><p className="small"><strong>{material(quote.level)}</strong> · {ru?'Прочность':'Strength'} ×{quote.strength} · {ru?'Повреждено секций':'Damaged sections'}: {village?.fortifications?.filter(f=>f.damage>0).length??quote.damaged}</p>
      <p className="small">{ru?'Уровни: деревянный забор → бревенчатый частокол (Плотницкое дело) → каменная стена (ещё и Горное дело). Улучшение усиливает весь забор и ворота.':'Levels: wooden fence → log palisade (Carpentry) → stone wall (also Mining). Upgrades strengthen all fence sections and gates.'}</p>
      {quote.nextLevel&&!site&&<><p className="small">{ru?'Следующий уровень':'Next level'}: {material(quote.nextLevel)}. {i18n.goods(quote.materials)} · {ru?'Строительной работы':'Construction work'}: {quote.work}</p>
        <p className="small muted">{ru?'Царь начинает улучшение, посадник отправляет заявку. Оно занимает одно место в общей очереди стройки. Затем жители приносят материалы и работают: Город → Помочь стройке → Улучшение укреплений. Само по себе оно не построится.':'The Tsar starts the upgrade; a Governor requests approval. It occupies one construction slot. Residents then deliver materials and work: Town → Help build → Fortification upgrade. It does not build itself.'}</p>
        <button disabled={busy||!!quote.blockedBy||!onVillage} onClick={async()=>{setBusy(true);setMessage('');try{onVillage?.(await api.upgradeFortifications(slug));setMessage(ru?(tsar?'Улучшение поставлено в очередь.':'Заявка отправлена царю.'):(tsar?'Upgrade queued.':'Request sent to the Tsar.'))}catch(e){setMessage(i18n.error(e))}finally{setBusy(false)}}}>{tsar?(ru?'Начать улучшение':'Start upgrade'):(ru?'Запросить улучшение':'Request upgrade')}</button>
      </>}
      {site&&<div className="fortification-work">
        <h4>{site.operation==='site-demolish'?(ru?'Разборка улучшения':'Dismantling the upgrade'):`${ru?'Строится':'Building'}: ${material(site.targetLevel??quote.nextLevel??quote.level)}`}</h4>
        <div className="bar gold"><div style={{width:`${Math.floor(site.progress*100)}%`}}/></div>
        {site.stages[site.stage]?.jobs.map(job=><p className="small" key={job.jobId}>{i18n.job(job.jobId)}: {i18n.amount(job.done)} / {i18n.amount(job.required)}</p>)}
        <p className="small">{ru?'Работы для жителей: Город → Помочь стройке → Улучшение укреплений.':'Resident jobs: Town → Help build → Fortification upgrade.'}</p>
        {!siteHasWork(site)&&site.operation!=='site-demolish'&&<p className="small muted">{ru?'Материалы ещё не вложены. Отмена чертежа: Стройка → Строится → Отменить чертёж.':'No materials have been invested. Cancel it under Construction → In progress → Withdraw blueprint.'}</p>}
        {(siteHasWork(site)||site.operation==='site-demolish')&&<SiteActions site={{...site,hasWork:siteHasWork(site)}} tsar={tsar} onAction={async operation=>{if(onVillage)onVillage(await api.civic(slug,site.plotId,operation))}}/>}
      </div>}
      <p className="small muted">{ru?'Улучшение не ремонтирует повреждения. Для ремонта жители выбирают работу «Ремонт зданий»; нужны дерево и камень. Готовое усиление во время набега применяется после конца всех волн.':'Upgrading does not repair existing damage. Residents repair it with the Repair buildings task, using wood and stone. During a raid the completed upgrade applies after all waves end.'}</p>
      {quote.readyAfterBattle?<p role="status">{ru?'Материалы и работа готовы. Усиление будет применено после набега; рабочие свободны.':'Materials and work are ready. Reinforcement will apply after the raid; workers are free.'}</p>:quote.blockedBy&&!site&&<p className="small muted">{i18n.error(new ApiError(400,quote.blockedBy,quote.blockedBy))}</p>}
    </>}
    {message&&<p role="status">{message}</p>}
  </section>
}
