import { useEffect, useRef, useState } from 'react'
import { api, createOperationId, type MonasteryView, type TilePoint } from '../api'
import { useI18n } from '../i18n'
export function MonasteryPanel({slug,onClose,onGo}:{slug:string;onClose:()=>void;onGo:(point:TilePoint)=>Promise<void>}) {
 const i18n=useI18n(),ru=i18n.language==='ru'
 const [view,setView]=useState<MonasteryView|null>(null),[error,setError]=useState(''),[busy,setBusy]=useState(false)
 const receipt=useRef<string|null>(null)
 useEffect(()=>{let alive=true;const load=()=>void api.monastery(slug).then(v=>{if(alive)setView(v)},e=>{if(alive)setError(i18n.error(e))});load();const timer=window.setInterval(load,3000);return()=>{alive=false;clearInterval(timer)}},[slug,i18n])
 const reason=!view?'…':!view.open?(ru?'Нужен Царский мост':'Build the Tsar Bridge'):!view.inRange?(ru?'Нужно прийти во двор':'Come to the courtyard'):!view.knowsRemedy?(ru?'Нужно травничество':'Discover herb lore'):view.readyAt&&Date.parse(view.readyAt)>Date.now()?(ru?'Следующий обмен':'Next contract')+' '+new Date(view.readyAt).toLocaleTimeString(i18n.language,{hour:'2-digit',minute:'2-digit'}):view.wood<10||view.food<6?(ru?'Не хватает припасов':'Not enough supplies'):view.remedyRoom<1?(ru?'Нет места для лекарства':'No room for a remedy'):''
 const trade=async()=>{if(busy||reason)return;setBusy(true);setError('');receipt.current??=createOperationId();try{setView(await api.monasteryTrade(slug,receipt.current));receipt.current=null}catch(e){setError(i18n.error(e))}finally{setBusy(false)}}
 return <div className="modal-backdrop"><section className="modal" role="dialog" aria-label={ru?'Монастырский двор':'Monastery courtyard'}><header className="modal-header"><h2>{ru?'Монастырский двор':'Monastery courtyard'}</h2><button className="close" onClick={onClose} aria-label={ru?'Закрыть':'Close'}>×</button></header><div className="modal-body">
 <p>{ru?'Церковь и кельи — нейтральный монастырь за рекой, а не дома деревни. Монахи готовят припарку за припасы.':'The church and cells belong to a neutral monastery, not the village. Monks prepare a remedy in exchange for supplies.'}</p>
 <p>{ru?'Поручение: принести 10 дерева и 6 еды → получить 1 лекарство. Одно поручение за 6 часов. Житель отдаёт личные припасы, царь — общие. Требуется изученное травничество.':'Contract: 10 wood and 6 food for 1 remedy, once per six hours. Residents use personal goods, the Tsar uses public stock. Herb lore is required.'}</p>
 {view?.open&&!view.inRange&&<button disabled={busy} onClick={()=>{if(!view)return;setBusy(true);void onGo(view.yard).then(onClose,e=>{setError(i18n.error(e));setBusy(false)})}}>{ru?'Отправиться во двор':'Travel to the courtyard'}</button>}
 <button disabled={busy||!!reason} onClick={()=>void trade()}>{ru?'Передать припасы':'Deliver supplies'}</button>{reason&&<p role="status">{reason}</p>}{error&&<p className="error" role="alert">{error}</p>}
 </div></section></div>
}
