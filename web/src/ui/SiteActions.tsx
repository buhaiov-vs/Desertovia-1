import {buildingSuffix} from '../buildingLabels'
import { useState } from 'react'
import type { Site } from '../api'
import { useI18n } from '../i18n'

export function SiteActions({site,actorId,tsar,onAction}:{site:Site;actorId?:string;tsar:boolean;onAction:(operation:string)=>Promise<void>}) {
  const i18n=useI18n(),ru=i18n.language==='ru'
  const [busy,setBusy]=useState(false),[confirm,setConfirm]=useState(false),[error,setError]=useState('')
  const own=!!actorId&&site.ownerAccountId===actorId
  const canManage=own||tsar&&(!site.ownerAccountId||site.boundaryConflict)
  const run=async(operation:string)=>{if(busy)return;setBusy(true);setError('');try{await onAction(operation);setConfirm(false)}catch(e){setError(i18n.error(e))}finally{setBusy(false)}}
  return <section className="site-management">
    <p className="small">{site.ownerAccountId?(ru?'Личная стройка':'Private construction'):(ru?'Общественная стройка':'Communal construction')}{buildingSuffix(site)}<br/>
      {ru?'Владелец / заявитель':'Owner / applicant'}: {site.ownerName??(ru?'не записан в старой версии':'not recorded in the old version')}
      {site.placedAt&&<><br/>{new Date(site.placedAt).toLocaleString(i18n.language)}</>}</p>
    {site.boundaryConflict&&<p className="warning small">{ru?'Старая личная площадка пересекает территорию царства. Решение принимает царь.':'A legacy private site overlaps the kingdom. The Tsar decides its status.'}</p>}
    {site.boundaryApproved&&<p className="small">{ru?'Личный участок согласован царём.':'Private plot approved by the Tsar.'}</p>}
    {site.operation==='site-demolish'?<p className="small">{ru?'Площадка разбирается. Возврат сохранится в кошельке владельца после завершения.':'Dismantling. Recovered materials will be kept for their owner after completion.'}</p>:canManage&&!site.operation&&<div className="site-actions">
      {site.boundaryConflict&&tsar&&<button disabled={busy} onClick={()=>void run('site-approve')}>{ru?'Согласовать личный участок':'Approve private plot'}</button>}
      {!site.hasWork?<button disabled={busy} onClick={()=>void run('site-cancel')}>{ru?'Убрать разметку':'Remove blueprint'}</button>:confirm?<><p className="small">{ru?'Разобрать площадку? Работа остановится. Вернётся 25% доставленных материалов первоначальному владельцу; забрать можно в кошельке.':'Dismantle? Site work will stop. 25% of delivered materials remain available to their original owner in the wallet.'}</p><button disabled={busy} onClick={()=>void run('site-demolish')}>{ru?'Подтвердить разборку':'Confirm dismantling'}</button><button disabled={busy} onClick={()=>setConfirm(false)}>{ru?'Отмена':'Cancel'}</button></>:<button disabled={busy} onClick={()=>setConfirm(true)}>{ru?'Разобрать площадку':'Dismantle site'}</button>}
    </div>}
    {error&&<p className="error" role="alert">{error}</p>}
  </section>
}
