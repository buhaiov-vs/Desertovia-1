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

export function raidSliderText(key: keyof RaidSettings, value: number, ru: boolean): string {
  const n = (x: number) => new Intl.NumberFormat(ru?'ru-RU':'en-US',{maximumFractionDigits:2}).format(x)
  if (key.endsWith('Multiplier')) return value===1 ? (ru?'×1 — обычное значение':'×1 — normal') : `×${n(value)} — ${n(value*100)}% ${ru?'от обычного':'of normal'}`
  if (key.endsWith('Seconds')) return value>=60 ? `${Math.floor(value/60)} ${ru?'мин':'min'}${value%60?` ${value%60} ${ru?'с':'s'}`:''}` : `${value} ${ru?'с':'s'}`
  if (key==='retreatSurvivorFraction') return value===0 ? (ru?'Не отступать по численности':'No survivor-count retreat') : `${n(value*100)}% ${ru?'врагов осталось':'enemies remaining'}`
  if (key==='theftFraction') return `${n(value*100)}% ${ru?'запаса ресурса':'of resource stock'}`
  if (key.endsWith('Percent')) return `${n(value)}%`
  if (key==='count') return `${ru?'До':'Up to'} ${n(value)} ${ru?'врагов в волне':'enemies per wave'}`
  if (key==='waves') return `${ru?'Волн':'Waves'}: ${n(value)}`
  if (key==='lootLimit') return `${n(value)} ${ru?'ед. ресурсов':'resource units'}`
  return `${n(value)} ${ru?'ед. урона':'damage points'}`
}

export function raidSliderHint(key: keyof RaidSettings, ru: boolean): string {
  const hints: Partial<Record<keyof RaidSettings,[string,string]>> = {
    count:['В авто это максимум: реальное число покажет предпросмотр. В ручном режиме — точное число.','Automatic mode uses this maximum; preview shows the real count. Manual mode uses this exact count.'],
    budgetMultiplier:['Общая сила набега относительно состояния деревни. Авто распределит её между числом и мощью врагов.','Total raid strength relative to the village. Automatic mode distributes it between count and enemy power.'],
    healthMultiplier:['Запас здоровья врагов. Например, ×2 делает их вдвое живучее.','Enemy health. For example, ×2 doubles their health.'],
    attackMultiplier:['Урон врагов по игрокам. Например, ×2 удваивает урон обычных ударов.','Enemy damage against players. For example, ×2 doubles ordinary hit damage.'],
    propertyMultiplier:['Урон врагов постройкам и укреплениям. Не меняет здоровье игроков.','Enemy damage against buildings and fortifications. Does not change player health.'],
    speedMultiplier:['Скорость передвижения врагов. ×0,5 — вдвое медленнее, ×2 — вдвое быстрее.','Enemy movement speed. ×0.5 is half speed; ×2 is double speed.'],
    weakPercent:['Доля слабых врагов. Остаток после слабых и сильных займут обычные.','Share of weak enemies. Regular enemies fill the remainder after weak and strong.'],
    elitePercent:['Доля сильных врагов. Вместе со слабыми не больше 100%.','Share of strong enemies. Weak and strong shares together cannot exceed 100%.'],
    waves:['Сколько групп придёт одна за другой.','How many successive groups will arrive.'],
    warningSeconds:['Время на подготовку до прихода первой волны.','Preparation time before the first wave arrives.'],
    waveIntervalSeconds:['Промежуток между объявленным приходом соседних волн.','Time between the scheduled arrivals of successive waves.'],
    durationSeconds:['Предельное время боя одной волны.','Maximum battle time for one wave.'],
    retreatSurvivorFraction:['При этой доле оставшихся врагов начинается отступление. Ноль отключает этот порог.','Retreat begins at this remaining-enemy share. Zero disables this threshold.'],
    propertyLimit:['Максимальный суммарный урон зданиям за волну. Ноль запрещает этот урон.','Maximum total building damage per wave. Zero prevents this damage.'],
    wallLimit:['Максимальный суммарный урон укреплениям за волну.','Maximum total fortification damage per wave.'],
    lootLimit:['Общий потолок украденных ресурсов за волну. Ноль отключает кражи.','Total stolen-resource cap per wave. Zero disables theft.'],
    theftFraction:['Доля выбранного ресурса на складе при краже. Применяется и общий предел краж.','Share of a selected stored resource stolen. The total theft cap also applies.'],
    bossWarningSeconds:['Сколько видно предупреждение перед тяжёлым ударом босса.','Warning time before the boss heavy strike.'],
    bossSecondWarningSeconds:['Предупреждение удара во второй фазе босса.','Strike warning time in the boss second phase.'],
    bossCooldownSeconds:['Пауза между тяжёлыми ударами босса.','Pause between boss heavy strikes.'],
    bossSecondCooldownSeconds:['Пауза между ударами во второй фазе босса.','Pause between strikes in the boss second phase.'],
  }
  return hints[key]?.[ru?0:1]??''
}
