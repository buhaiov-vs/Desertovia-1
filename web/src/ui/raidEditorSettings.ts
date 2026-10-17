import type { EnemyKind, RaidSettings } from '../api'

export function equalRaidShares(enemies: EnemyKind[]): RaidSettings['species'] {
  return enemies.map((enemy, index) => ({ enemy, percent: Math.floor(100 / enemies.length) + (index < 100 % enemies.length ? 1 : 0) }))
}

/** Mirrors the owner's server ranges. Invalid drafts never reach preview/save/launch. */
export function raidSettingErrors(s: RaidSettings, ru: boolean): Partial<Record<keyof RaidSettings, string>> {
  const errors: Partial<Record<keyof RaidSettings, string>> = {}
  const range = (key: keyof RaidSettings, label: string, min: number, max: number, integer = false) => {
    const value = Number(s[key])
    if (!Number.isFinite(value) || value < min || value > max || (integer && !Number.isInteger(value)))
      errors[key] = ru ? `${label}: ${integer ? 'целое число ' : ''}от ${min} до ${max}.` : `${label}: ${integer ? 'a whole number ' : ''}from ${min} to ${max}.`
  }
  const r = (key: keyof RaidSettings, ruLabel: string, enLabel: string, min: number, max: number, integer = false) => range(key, ru ? ruLabel : enLabel, min, max, integer)
  r('count','Врагов в волне','Enemies per wave',1,s.preset==='boss'?3:48,true)
  r('waves','Волн','Waves',1,5,true)
  r('warningSeconds','Предупреждение, сек','Warning, seconds',30,900,true)
  r('waveIntervalSeconds','Интервал волн, сек','Wave interval, seconds',15,600,true)
  r('durationSeconds','Длительность боя, сек','Fight duration, seconds',30,360,true)
  r('budgetMultiplier','Множитель бюджета','Budget multiplier',.5,3)
  r('healthMultiplier','Здоровье ×','Health ×',.25,20)
  r('attackMultiplier','Урон игрокам ×','Player damage ×',.25,10)
  r('propertyMultiplier','Урон имуществу ×','Property damage ×',.25,5)
  r('speedMultiplier','Скорость ×','Speed ×',.25,2)
  r('weakPercent','Слабых, %','Weak, %',0,100,true)
  r('elitePercent','Сильных, %','Strong, %',0,100,true)
  r('retreatSurvivorFraction','Доля выживших для отступления','Retreat survivor fraction',0,.8)
  r('propertyLimit','Предел урона зданиям','Building damage limit',0,1000,true)
  r('wallLimit','Предел урона стенам','Wall damage limit',0,1500,true)
  r('lootLimit','Предел украденных ресурсов','Stolen goods limit',0,500,true)
  r('theftFraction','Доля ресурса при краже','Theft fraction',0,.1)
  r('bossWarningSeconds','Предупреждение удара, сек','Strike warning, seconds',1,10,true)
  r('bossSecondWarningSeconds','Предупреждение второй фазы, сек','Phase two warning, seconds',1,10,true)
  r('bossCooldownSeconds','Перерыв ударов, сек','Strike cooldown, seconds',3,30,true)
  r('bossSecondCooldownSeconds','Перерыв второй фазы, сек','Phase two cooldown, seconds',3,30,true)
  if (s.weakPercent+s.elitePercent>100) errors.weakPercent=ru?'Слабых и сильных вместе больше 100%. Уменьши одну из долей; остаток — обычные враги.':'Weak and strong total over 100%. Reduce one share; the rest are regular enemies.'
  const total=s.species.reduce((sum,p)=>sum+p.percent,0)
  if(s.species.length>0 && (total!==100 || s.species.some(p=>!Number.isInteger(p.percent)||p.percent<1||p.percent>100)))
    errors.species=ru?`Состав видов: сейчас ${total}%, нужно 100%. Нажми «Поровну» или измени доли.`:`Species total: ${total}%; 100% required. Choose “Equal shares” or edit the shares.`
  return errors
}
