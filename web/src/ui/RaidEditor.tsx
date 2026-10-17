import { useEffect, useState } from 'react'
import { api, createOperationId, type EnemyKind, type EnemyDefinition, type RaidEditorView, type RaidPreview, type RaidSettings, type VillageState } from '../api'
import { useI18n } from '../i18n'

function raidPreset(preset:'boss'|'mixed'|'swarm'):RaidSettings {
  return {preset,mode:'automatic',enemy:'Zombie',sector:null,count:preset==='boss'?2:preset==='swarm'?48:24,
    waves:1,warningSeconds:180,waveIntervalSeconds:120,durationSeconds:preset==='boss'?360:240,
    budgetMultiplier:1,healthMultiplier:1,attackMultiplier:1,propertyMultiplier:1,speedMultiplier:1,
    weakPercent:preset==='swarm'?100:35,elitePercent:preset==='swarm'?0:20,tactic:preset==='swarm'?'breach':'mixed',
    retreatSurvivorFraction:preset==='boss'?0:.25,propertyLimit:360,wallLimit:600,lootLimit:24,theftFraction:.02,
    bossStrike:true,bossWarningSeconds:3,bossSecondWarningSeconds:2,bossCooldownSeconds:9,bossSecondCooldownSeconds:6,
    bossSecondPhase:true,species:[]}
}
const kinds:EnemyKind[]=['Wolf','Boar','Bear','Zombie','Skeleton']

export function RaidEditor({village,pending,onVillage,enemies}:{village:VillageState;pending:boolean;onVillage:(v:VillageState)=>void;enemies?:readonly EnemyDefinition[]}) {
  const availableKinds=enemies?.filter(e=>!e.dungeonOnly).map(e=>e.kind)??kinds
  const i18n=useI18n(),ru=i18n.language==='ru'
  const [settings,setSettings]=useState<RaidSettings>(()=>raidPreset('mixed'))
  const [view,setView]=useState<RaidEditorView|null>(null)
  const [preview,setPreview]=useState<RaidPreview|null>(null)
  const [error,setError]=useState<string|null>(null)
  const [busy,setBusy]=useState(false)
  const [name,setName]=useState(''),[presetId,setPresetId]=useState<string|null>(null)
  const [requestId,setRequestId]=useState(createOperationId)
  const [previewKey,setPreviewKey]=useState('')
  const disabled=pending||busy
  useEffect(()=>{let stopped=false;api.raidEditor(village.slug).then(v=>{if(!stopped)setView(v)}).catch(e=>{if(!stopped)setError(i18n.error(e))});return()=>{stopped=true}},[village.slug,i18n])
  const change=(patch:Partial<RaidSettings>)=>{setSettings(s=>({...s,...patch}));setPreview(null);setPreviewKey('');setRequestId(createOperationId());setError(null)}
  const load=(next:RaidSettings)=>{setSettings(structuredClone(next));setPreview(null);setPreviewKey('');setRequestId(createOperationId());setError(null)}
  async function run(action:()=>Promise<void>){setBusy(true);setError(null);try{await action()}catch(e){setError(i18n.error(e))}finally{setBusy(false)}}
  const number=(key:keyof RaidSettings,label:string,min:number,max:number,step=1)=><label className="raid-editor-field" key={key}>{label}<input aria-label={label} type="number" min={min} max={max} step={step} disabled={disabled} value={Number(settings[key])} onChange={e=>change({[key]:Number(e.target.value)})}/></label>
  const select=(key:'enemy'|'sector'|'tactic',label:string,options:[string,string][])=><label className="raid-editor-field">{label}<select aria-label={label} disabled={disabled} value={settings[key]??''} onChange={e=>change({[key]:e.target.value||null})}>{options.map(([v,l])=><option key={v} value={v}>{l}</option>)}</select></label>
  return <div className="raid-editor">
    <h4>{ru?'Настроить набег':'Raid editor'}</h4>
    <div className="chips" role="group" aria-label={ru?'Пресет набега':'Raid preset'}>
      {(['boss','mixed','swarm'] as const).map(p=><button key={p} disabled={disabled} className={`chip ${settings.preset===p?'active':''}`} onClick={()=>{load(raidPreset(p));setPresetId(null)}}>{p==='boss'?'БОСС-ФАЙТ':p==='mixed'?(ru?'Смешанный бой':'Mixed'):(ru?'Орда':'Horde')}</button>)}
    </div>
    <label className="raid-editor-field">{ru?'Расчёт силы':'Strength calculation'}<select disabled={disabled} value={settings.mode} onChange={e=>change({mode:e.target.value as RaidSettings['mode']})}>
      <option value="automatic">{ru?'Автоматический бюджет':'Automatic budget'}</option><option value="manual">{ru?'Ручной — только владелец':'Manual — owner only'}</option>
    </select></label>
    <p className="small muted">{ru?'Авто ограничивает состав бюджетом. Ручной режим запускает выбранное число и силу, даже выше рекомендации; его результаты не меняют адаптацию.':'Automatic mode fits the budget. Manual mode uses the exact count and strength, even above the recommendation; it does not affect adaptation.'}</p>
    {view&&<p className="small">{ru?'Адаптация':'Adaptation'}: {Math.round(view.coefficient*100)}% · {view.reason}</p>}
    <div className="raid-editor-grid">
      {select('enemy',ru?'Основной вид':'Main species',availableKinds.map(k=>[k,i18n.enemy(k)]))}
      {number('count',ru?'Врагов в волне (максимум в авто)':'Enemies per wave (maximum in auto)',1,settings.preset==='boss'?3:48)}
      {select('sector',ru?'Сторона подхода':'Approach',[['',ru?'Автовыбор':'Automatic'],['north',ru?'Север':'North'],['west',ru?'Запад':'West'],['south',ru?'Юг':'South']])}
      {select('tactic',ru?'Тактика':'Tactic',[['mixed',ru?'Смешанная':'Mixed'],['entry',ru?'Искать вход':'Seek entrance'],['breach',ru?'Проламывать':'Breach'],['plunder',ru?'Грабить':'Plunder'],['hunt',ru?'Давить на игроков':'Hunt players']])}
      {settings.mode==='automatic'&&number('budgetMultiplier',ru?'Множитель бюджета':'Budget multiplier',.5,1.5,.05)}
      {number('healthMultiplier',ru?'Здоровье ×':'Health ×',.25,20,.05)}
      {number('attackMultiplier',ru?'Урон игрокам ×':'Player damage ×',.25,10,.05)}
      {number('propertyMultiplier',ru?'Урон имуществу ×':'Property damage ×',.25,5,.05)}
      {number('speedMultiplier',ru?'Скорость ×':'Speed ×',.25,2,.05)}
    </div>
    <details><summary>{ru?'Состав, волны и ограничения':'Composition, waves and limits'}</summary>
      <div className="raid-editor-grid">
        {settings.preset==='mixed'&&number('weakPercent',ru?'Слабых, %':'Weak, %',0,100)}
        {settings.preset==='mixed'&&number('elitePercent',ru?'Сильных, %':'Strong, %',0,100)}
        {number('waves',ru?'Волн':'Waves',1,5)}{number('warningSeconds',ru?'Предупреждение, сек':'Warning, seconds',30,900)}
        {number('waveIntervalSeconds',ru?'Интервал волн, сек':'Wave interval, seconds',15,600)}
        {number('durationSeconds',ru?'Длительность боя, сек':'Fight duration, seconds',30,360)}
        {number('retreatSurvivorFraction',ru?'Отступать при доле выживших':'Retreat at survivor fraction',0,.8,.05)}
        {number('propertyLimit',ru?'Предел урона зданиям':'Building damage limit',0,1000)}
        {number('wallLimit',ru?'Предел урона стенам':'Wall damage limit',0,1500)}
        {number('lootLimit',ru?'Предел украденных ресурсов':'Stolen goods limit',0,500)}
        {number('theftFraction',ru?'Доля ресурса при краже (0,02 = 2%)':'Theft fraction (0.02 = 2%)',0,.1,.01)}
      </div>
      <p className="small">{ru?'Смешать виды (доли должны составить 100%). Без отметок используется основной вид.':'Mix species (shares must total 100%). With none selected, the main species is used.'}</p>
      {availableKinds.map(kind=>{const part=settings.species.find(s=>s.enemy===kind);return <div className="raid-editor-species" key={kind}>
        <label><input type="checkbox" disabled={disabled} checked={!!part} onChange={e=>change({species:e.target.checked?[...settings.species,{enemy:kind,percent:100}]:settings.species.filter(s=>s.enemy!==kind)})}/>{i18n.enemy(kind)}</label>
        {part&&<input aria-label={`${i18n.enemy(kind)} %`} disabled={disabled} type="number" min={1} max={100} value={part.percent} onChange={e=>change({species:settings.species.map(s=>s.enemy===kind?{...s,percent:Number(e.target.value)}:s)})}/>}
      </div>})}
    </details>
    {settings.preset==='boss'&&<details><summary>{ru?'Удары и фазы босса':'Boss strikes and phases'}</summary>
      <label><input type="checkbox" checked={settings.bossStrike} disabled={disabled} onChange={e=>change({bossStrike:e.target.checked})}/>{ru?'Тяжёлый удар по площади':'Heavy area strike'}</label>
      <label><input type="checkbox" checked={settings.bossSecondPhase} disabled={disabled} onChange={e=>change({bossSecondPhase:e.target.checked})}/>{ru?'Вторая фаза ниже 50% HP':'Second phase below 50% HP'}</label>
      <div className="raid-editor-grid">
        {number('bossWarningSeconds',ru?'Предупреждение удара, сек':'Strike warning, seconds',1,10)}
        {number('bossCooldownSeconds',ru?'Перерыв ударов, сек':'Strike cooldown, seconds',3,30)}
        {number('bossSecondWarningSeconds',ru?'Предупреждение во второй фазе, сек':'Phase two warning, seconds',1,10)}
        {number('bossSecondCooldownSeconds',ru?'Перерыв во второй фазе, сек':'Phase two cooldown, seconds',3,30)}
      </div>
    </details>}
    <div className="raid-editor-presets">
      <label>{ru?'Сохранённые пресеты':'Saved presets'}<select disabled={disabled} value={presetId??''} onChange={e=>{const p=view?.presets.find(p=>p.id===e.target.value);if(p){load(p.settings);setName(p.name);setPresetId(p.id)}else{setPresetId(null);setName('')}}}>
        <option value="">{ru?'Новый пресет':'New preset'}</option>{view?.presets.map(p=><option key={p.id} value={p.id}>{p.name}</option>)}
      </select></label>
      <input aria-label={ru?'Название пресета':'Preset name'} placeholder={ru?'Название пресета':'Preset name'} maxLength={48} value={name} disabled={disabled} onChange={e=>setName(e.target.value)}/>
      <button disabled={disabled||!name.trim()} onClick={()=>void run(async()=>{const saved=await api.saveRaidPreset(village.slug,presetId,name,settings);setView(saved);if(!presetId)setPresetId(saved.presets.at(-1)?.id??null)})}>{ru?'Сохранить':'Save'}</button>
    </div>
    <button disabled={disabled} onClick={()=>void run(async()=>{const p=await api.previewRaid(village.slug,settings);setPreview(p);setPreviewKey(JSON.stringify(settings))})}>{ru?'Предпросмотр':'Preview'}</button>
    {preview&&<div className="raid-preview" role="status">
      <strong>{ru?'Будет в каждой волне':'Each wave'}: {preview.count} · HP {Math.round(preview.totalHealth)} · {ru?'суммарная атака':'total attack'} {Math.round(preview.totalAttack)}</strong>
      <p className="small">{ru?'Сила / рекомендованный бюджет':'Strength / recommended budget'}: {preview.actualCost.toFixed(1)} / {preview.recommendedBudget.toFixed(1)} · {preview.risk==='dangerous'?(ru?'Опасный бой':'Dangerous fight'):preview.risk==='weak'?(ru?'Слабый бой':'Weak fight'):(ru?'Сопоставимый бой':'Comparable fight')}</p>
      <p className="small">{ru?'Состав':'Composition'}: {[...new Set(preview.units.map(u=>`${i18n.enemy(u.enemy)} · ${ru?({weak:'Слабый',regular:'Обычный',strong:'Сильный',boss:'Босс'} as Record<string,string>)[u.variant]:u.variant}`))].map(key=>`${key} × ${preview.units.filter(u=>`${i18n.enemy(u.enemy)} · ${ru?({weak:'Слабый',regular:'Обычный',strong:'Сильный',boss:'Босс'} as Record<string,string>)[u.variant]:u.variant}`===key).length}`).join('; ')}</p>
      {settings.mode==='manual'&&<p className="error">{ru?'Ручной эксперимент: ограничение автоматическим бюджетом отключено.':'Manual experiment: the automatic budget limit is disabled.'}</p>}
      <button className="primary" disabled={disabled||previewKey!==JSON.stringify(settings)||village.raids.some(r=>r.outcome==='Pending')} onClick={()=>void run(async()=>{onVillage(await api.launchRaid(village.slug,requestId,settings));setPreview(null)})}>{ru?'Объявить набег':'Announce raid'}</button>
    </div>}
    {error&&<p role="alert" className="error">{error}</p>}
  </div>
}
