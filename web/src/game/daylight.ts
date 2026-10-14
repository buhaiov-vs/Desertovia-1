import type { Weather } from '../api'

/** Share of a daytime or night the light takes to change, on each side of dusk and dawn. */
const TWILIGHT = 0.08

const clamp01 = (value: number) => Math.min(1, Math.max(0, value))
const smooth = (value: number) => value * value * (3 - 2 * value)

/**
 * How dark the land is at `now`: 0 at full daylight, 1 deep in the night. The light is half-way at every dusk and dawn,
 * so it changes smoothly across the boundary although the server switches the period at once.
 */
export function darknessAt(weather: Weather, now: number) {
  const start = Date.parse(weather.periodStartedAt)
  const progress = clamp01((now - start) / (Date.parse(weather.periodEndsAt) - start))
  const settled = smooth(clamp01(progress / TWILIGHT))
  const turning = smooth(clamp01((progress - (1 - TWILIGHT)) / TWILIGHT))
  const level = weather.night ? 1 : 0
  return 0.5 + (level - 0.5) * (settled - turning)
}
