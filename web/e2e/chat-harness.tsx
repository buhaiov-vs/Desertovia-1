// Test-only React stand: real ChatPanel without server writes or unrelated Pixi workloads.
import {useState} from 'react'
import {createRoot} from 'react-dom/client'
import {ChatPanel} from '../src/ui/ChatPanel'
import {I18nProvider} from '../src/i18n'
import type {ChatMessage} from '../src/api'
import '../src/index.css'
const msg=(id:string,text:string,own=false):ChatMessage=>({id,text,authorName:own?'Я':'Житель',citizenId:own?'me':'other',isTsar:false,isChaplain:false,sentAt:new Date().toISOString()})
function Stand(){
 const [messages,setMessages]=useState<ChatMessage[]>(Array.from({length:70},(_,i)=>msg('history-'+i,'Старая история '+i)))
 const [incoming,setIncoming]=useState<ChatMessage[]>([])
 const [sent,setSent]=useState('')
 const [interrupted,setInterrupted]=useState(false)
 const add=(own=false)=>{const m=msg(crypto.randomUUID(),own?'Своё сообщение':'Новое сообщение игрока',own);setMessages(previous=>[...previous,m]);setIncoming(previous=>[...previous,m])}
 return <I18nProvider><div className="game-root"><div className="game-canvas" style={{background:'#83b765'}}/><div className="hud">
  <div className="hud-top"><strong>Дезертовия</strong></div>
  <ChatPanel interrupted={interrupted} feed={[]} messages={messages} incoming={incoming} member tsar={false} ownCitizenId="me" serverOffsetMs={0} phone={matchMedia('(max-width:720px)').matches} onSend={async text=>{setSent(text);add(true)}}/>
  <div className="hud-bottom"><div className="hud-status"><button>Строить</button><button>Персонаж</button></div></div>
 </div>{interrupted&&<div className="modal-backdrop"><section className="modal poll"><h2>Голосование</h2><button onClick={()=>setInterrupted(false)}>Закрыть голосование</button></section></div>}</div><div style={{position:'fixed',top:80,right:10,zIndex:1002}}>
  <button id="overlay" onClick={()=>setInterrupted(true)}>Open vote</button><button id="arrive" onClick={()=>add()}>Incoming</button><button id="own" onClick={()=>add(true)}>Own</button>
  <button id="history" onClick={()=>setMessages(previous=>[...previous,msg('late-history','Догруженная история')])}>History</button><output id="sent">{sent}</output>
 </div></I18nProvider>
}
createRoot(document.getElementById('app')!).render(<Stand/>);
