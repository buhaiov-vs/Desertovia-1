/** One HTML audio element. Calm melodies advance when each file ends; a village raid replaces the current one with battle.mp3. */
const CALM = ['part_001.mp3', 'part_002.mp3', 'part_003.mp3', 'part_004.mp3', 'part_005.mp3', 'part_006.mp3', 'part_007.mp3']
const BATTLE = 'battle.mp3'

export interface MusicSnapshot {
  src: string
  paused: boolean
  loop: boolean
  mode: 'calm' | 'battle'
  raid: boolean
  unlocked: boolean
  readyState: number
  error: number
}

/** A raid counts once fighters or enemies are in the village. The warning countdown stays on the calm melody. */
export function cityRaidPlaying(battles: readonly { finishedAt: string | null; phase: string }[] | undefined) {
  return !!battles?.some((battle) => !battle.finishedAt && battle.phase !== 'Warning' && battle.phase !== 'Cancelled')
}

class BackgroundMusic {
  private readonly audio = new Audio()
  private readonly calm: string[]
  private readonly battle: string
  private cursor = 0
  private calmTime = 0
  private mode: 'calm' | 'battle' = 'calm'
  private raid = false
  private muted = false
  private volume = 0.4
  private unlocked = false
  private users = 0
  private misses = 0
  private token = 0
  private readonly listeners = new Set<() => void>()

  private notify() {
    for (const listener of this.listeners) listener()
  }

  constructor() {
    const start = Math.floor(Math.random() * CALM.length)
    const order = CALM.slice(start).concat(CALM.slice(0, start))
    this.calm = order.map((file) => this.fileUrl(file))
    this.battle = this.fileUrl(BATTLE)
    this.audio.preload = 'none'
    this.audio.addEventListener('ended', this.onEnded)
    this.audio.addEventListener('playing', () => { this.misses = 0; this.notify() })
    this.audio.addEventListener('pause', () => this.notify())
    this.audio.addEventListener('error', this.onError)
    document.addEventListener('visibilitychange', this.onVisibility)
  }

  attach() {
    this.users += 1
    if (this.unlocked && !this.silent()) void this.follow()
  }

  detach() {
    this.users = Math.max(0, this.users - 1)
    if (this.users === 0) this.audio.pause()
  }

  subscribe(listener: () => void) {
    this.listeners.add(listener)
    return () => { this.listeners.delete(listener) }
  }

  /** Must run inside a user gesture the first time, so the browser allows playback. */
  unlock() {
    this.unlocked = true
    this.notify()
    if (!this.silent()) void this.follow()
  }

  apply(options: { raid: boolean; muted: boolean; volume: number }) {
    this.raid = options.raid
    this.muted = options.muted
    this.volume = options.volume
    this.audio.volume = this.level()
    if (!this.unlocked) return
    if (this.silent()) {
      this.audio.pause()
      return
    }
    void this.follow()
  }

  snapshot(): MusicSnapshot {
    return {
      src: this.audio.currentSrc || this.audio.src,
      paused: this.audio.paused,
      loop: this.audio.loop,
      mode: this.mode,
      raid: this.raid,
      unlocked: this.unlocked,
      readyState: this.audio.readyState,
      error: this.audio.error?.code ?? 0,
    }
  }

  private fileUrl(file: string) {
    return `${import.meta.env.BASE_URL}assets/music/${file}`
  }

  private level() {
    return this.muted ? 0 : this.volume
  }

  private silent() {
    return this.muted || this.volume <= 0 || this.users === 0 || document.hidden
  }

  private calmUrl() {
    return this.calm[this.cursor] ?? this.calm[0]
  }

  private onEnded = () => {
    if (this.mode !== 'calm' || this.raid || this.silent()) return
    this.calmTime = 0
    this.cursor = (this.cursor + 1) % this.calm.length
    void this.play(this.calmUrl(), false, 0)
  }

  private onError = () => {
    if (this.mode !== 'calm' || this.audio.error?.code === MediaError.MEDIA_ERR_ABORTED) return
    this.misses += 1
    if (this.misses > this.calm.length) {
      this.audio.pause()
      return
    }
    this.cursor = (this.cursor + 1) % this.calm.length
    this.calmTime = 0
    void this.play(this.calmUrl(), false, 0)
  }

  private onVisibility = () => {
    if (!this.unlocked) return
    if (document.hidden) this.audio.pause()
    else if (!this.silent()) void this.follow()
  }

  private async follow() {
    const raid = this.raid
    const playingRaid = this.mode === 'battle' && this.audio.src.includes(BATTLE)
    const playingCalm = this.mode === 'calm' && this.audio.src.includes('/assets/music/part_')
    if ((raid && playingRaid) || (!raid && playingCalm)) {
      this.audio.volume = this.level()
      if (this.audio.paused) await this.audio.play().catch(() => undefined)
      return
    }
    if (this.mode === 'calm' && this.audio.currentSrc.includes('/assets/music/part_')) this.calmTime = this.audio.currentTime
    this.mode = raid ? 'battle' : 'calm'
    await this.play(raid ? this.battle : this.calmUrl(), raid, raid ? 0 : this.calmTime)
  }

  private async play(url: string, loop: boolean, at: number) {
    const token = ++this.token
    const absolute = new URL(url, window.location.href).href
    this.audio.loop = loop
    this.audio.volume = this.level()
    if (this.audio.src !== absolute) this.audio.src = url
    if (at > 0.25) {
      const seek = () => {
        if (token !== this.token || !Number.isFinite(this.audio.duration)) return
        this.audio.currentTime = Math.min(at, Math.max(0, this.audio.duration - 0.5))
      }
      if (this.audio.readyState >= 1) seek()
      else this.audio.addEventListener('loadedmetadata', seek, { once: true })
    }
    if (token !== this.token || this.silent()) return
    await this.audio.play().catch(() => undefined)
  }
}

let singleton: BackgroundMusic | null = null

export function backgroundMusic() {
  singleton ??= new BackgroundMusic()
  return singleton
}
