import { describe, expect, it } from 'vitest'
import { currentCombatHealth } from '../src/combatHealth'

describe('currentCombatHealth', () => {
  const base = {
    health: 16,
    maximumHealth: 32,
    healthUpdatedAt: '2026-11-01T12:00:00.000Z',
    serverNow: '2026-11-01T12:00:00.000Z',
  }
  const start = Date.parse(base.healthUpdatedAt)

  it('stays at the stored value at the snapshot time', () => {
    expect(currentCombatHealth(base, start, 30)).toBe(16)
  })

  it('fills the missing half after fifteen minutes from half health', () => {
    expect(currentCombatHealth(base, start + 7.5 * 60_000, 30)).toBe(24)
    expect(currentCombatHealth(base, start + 15 * 60_000, 30)).toBe(32)
  })

  it('is full after thirty minutes from empty', () => {
    const empty = { ...base, health: 0 }
    expect(currentCombatHealth(empty, start + 15 * 60_000, 30)).toBe(16)
    expect(currentCombatHealth(empty, start + 30 * 60_000, 30)).toBe(32)
  })

  it('does not go above the maximum', () => {
    expect(currentCombatHealth(base, start + 60 * 60_000, 30)).toBe(32)
  })
})
