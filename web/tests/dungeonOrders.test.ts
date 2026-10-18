import { test } from 'node:test'
import assert from 'node:assert/strict'
import { DungeonOrders, type DungeonOrder } from '../src/game/dungeonOrders.ts'

function stand() {
  const sent: DungeonOrder[] = []
  const replies: { resolve: (value: boolean) => void; reject: (reason: Error) => void }[] = []
  const orders = new DungeonOrders((order) => {
    sent.push(order)
    return new Promise<boolean>((resolve, reject) => replies.push({ resolve, reject }))
  })
  return { orders, sent, replies }
}
const flush = () => new Promise<void>((resolve) => setImmediate(resolve))

test('a burst sends the first command and the latest intent, without a stale-click backlog', async () => {
  const s = stand()
  s.orders.offer({ method: 'Attack', args: [1] })
  for (let i = 0; i < 100; i++) s.orders.offer({ method: 'MoveTo', args: [i, i] })
  s.orders.offer({ method: 'Heal', args: ['ally'] })
  assert.deepEqual(s.sent, [{ method: 'Attack', args: [1] }])
  s.replies[0].resolve(true)
  await flush()
  assert.deepEqual(s.sent, [{ method: 'Attack', args: [1] }, { method: 'Heal', args: ['ally'] }])
  s.replies[1].resolve(true)
  await flush()
  assert.equal(s.sent.length, 2)
})

test('a rejected old target does not swallow a newer move', async () => {
  const s = stand()
  s.orders.offer({ method: 'Attack', args: [1] })
  s.orders.offer({ method: 'MoveTo', args: [4, 5] })
  s.replies[0].resolve(false)
  await flush()
  assert.deepEqual(s.sent[1], { method: 'MoveTo', args: [4, 5] })
  s.replies[1].resolve(true)
})

test('disconnect clears pending commands and never retries a lost order', async () => {
  const s = stand()
  s.orders.offer({ method: 'Attack', args: [1] })
  s.orders.offer({ method: 'Attack', args: [2] })
  s.orders.clear()
  s.replies[0].reject(new Error('disconnected'))
  await flush()
  assert.equal(s.sent.length, 1)
  s.orders.offer({ method: 'MoveTo', args: [8, 9] })
  assert.deepEqual(s.sent[1], { method: 'MoveTo', args: [8, 9] })
  s.replies[1].resolve(true)
})

test('leaving a run disposes the queue even when an old acknowledgement arrives later', async () => {
  const s = stand()
  s.orders.offer({ method: 'Attack', args: [1] })
  s.orders.offer({ method: 'MoveTo', args: [1, 2] })
  s.orders.dispose()
  s.replies[0].resolve(true)
  await flush()
  s.orders.offer({ method: 'Heal', args: ['someone-in-the-next-run'] })
  assert.equal(s.sent.length, 1)
})
