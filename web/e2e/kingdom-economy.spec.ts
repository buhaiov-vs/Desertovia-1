import { test, expect } from '@playwright/test'

test('economy controls fit phone and desktop in both themes; failed purchase retains draft and request identity',async({page})=>{
  await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))
  await page.setViewportSize({width:320,height:760})
  await page.goto('/e2e/economy-harness.html')
  await page.getByText('Кошелёк, имущество и рынок',{exact:true}).click()
  await expect(page.locator('.economy-balances')).toContainText('1000')
  const amount=page.getByLabel('Количество',{exact:true})
  await amount.fill('');await expect(amount).toHaveValue('');await amount.fill('2')
  await page.evaluate(()=>{(window as unknown as {failEconomyOnce:boolean}).failEconomyOnce=true})
  await page.getByRole('button',{name:/Купить у царства/}).click()
  await expect(page.getByRole('alert')).toContainText('Обрыв связи')
  await expect(amount).toHaveValue('2')
  await page.getByRole('button',{name:/Купить у царства/}).click()
  await expect(page.locator('.economy-balances')).toContainText('996')
  const ids=await page.evaluate(()=>(window as unknown as {economyCalls:{requestId:string}[]}).economyCalls.map(r=>r.requestId))
  expect(ids).toHaveLength(2);expect(ids[0]).toBe(ids[1])
  await page.locator('.economy-panel details summary').evaluateAll(elements=>elements.forEach(el=>(el.parentElement as HTMLDetailsElement).open=true))
  for(const width of [320,1440])for(const theme of ['light','dark']){
    await page.setViewportSize({width,height:900})
    await page.evaluate(value=>document.documentElement.dataset.uiTheme=value,theme)
    expect(await page.locator('.economy-panel').evaluate(el=>el.scrollWidth<=el.clientWidth)).toBe(true)
    for(const button of await page.locator('.economy-panel button').all()){
      const box=await button.boundingBox();if(box){expect(box.x).toBeGreaterThanOrEqual(0);expect(box.x+box.width).toBeLessThanOrEqual(width)}
    }
  }
})

test('disabled in-game popups keep unread counts and selected mentions use stable IDs',async({page})=>{
  await page.addInitScript(()=>{localStorage.setItem('desertod.language','ru');localStorage.setItem('desertod.chat.mode','hidden');localStorage.setItem('desertovia.gamePopups','0')})
  await page.goto('/e2e/chat-harness.html')
  await page.locator('#arrive').click()
  await expect(page.locator('.chat-launch .hud-card-count')).toHaveText('1')
  await expect(page.locator('.chat-toast')).toHaveCount(0)
  await page.getByRole('button',{name:'Открыть чат',exact:true}).click()
  await page.getByRole('button',{name:'Упомянуть жителя'}).click()
  await page.getByRole('button',{name:'@Адресат',exact:true}).click()
  const input=page.getByRole('textbox',{name:'Скажите что-нибудь племени…'})
  await expect(input).toHaveValue('@Адресат ')
  await input.fill('@Адресат привет')
  await page.getByRole('button',{name:'Отправить',exact:true}).click()
  await expect(page.locator('#sent-mentions')).toHaveText('stable-recipient-id')
})


test('map placement uses the expanded kingdom and keeps private construction outside it and off the river',async({page})=>{
  await page.goto('/e2e/economy-harness.html')
  const actual=await page.evaluate(()=>{
    const can=(window as unknown as {kingdomCanBuild:(p:boolean,x:number,y:number)=>boolean}).kingdomCanBuild
    return [can(false,-4,10),can(true,-4,10),can(false,-8,10),can(true,-8,10),can(true,30,14)]
  })
  expect(actual).toEqual([true,false,false,true,false])
})

test('third tired work warns about fainting before starting on a narrow screen',async({page})=>{
  await page.addInitScript(()=>localStorage.setItem('desertod.language','ru'))
  await page.setViewportSize({width:320,height:760})
  await page.goto('/e2e/economy-harness.html')
  await expect(page.locator('#faint-warning')).toContainText('После этой работы — обморок на 12 игровых часов')
  expect(await page.locator('#faint-warning').evaluate(el=>el.scrollWidth<=el.clientWidth)).toBe(true)
})
