import {createRoot} from 'react-dom/client'
import {useState} from 'react'
import {RescuePatientPanel, type RescuePatientView} from '../src/ui/RescuePatientPanel'
import '../src/index.css'

const now=Date.now()
function Harness(){
  const [mode,setMode]=useState('own')
  const [requested,setRequested]=useState(false)
  const [calls,setCalls]=useState(0)
  const patient:RescuePatientView={name:'Раненый с длинным именем',injuredUntil:new Date(now+40*60_000).toISOString(),
    rescueRequestedAt:requested?new Date(now).toISOString():null,
    hospitalizedAt:mode==='ward'?new Date(now).toISOString():null,
    rescueByCitizenId:mode==='carrying'?'helper':null}
  return <><nav style={{position:'relative',zIndex:3000}}>
    {['own','other','missing','ward','carrying'].map(m=><button key={m} onClick={()=>setMode(m)}>{m}</button>)}
    <output id="calls">{calls}</output>
  </nav><RescuePatientPanel patient={patient} now={now} pending={false} hasHospital={mode!=='missing'} own={mode==='own'} canCarry={mode!=='own'}
    carrierName="Спасатель" onClose={()=>{}} onCarry={()=>setCalls(c=>c+1)} onAsk={()=>{setRequested(true);setCalls(c=>c+1)}}/></>
}
createRoot(document.getElementById('app')!).render(<Harness/> )
