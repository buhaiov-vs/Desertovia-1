/** Capture hooks exist only while a renderer is mounted. Images are created only on an explicit click. */
const sources = new Map<HTMLCanvasElement, {capture: () => HTMLCanvasElement; obscure: (visible:boolean) => void}>()
let reporting = false
export function setReporting(visible: boolean) {
  reporting = visible
  for (const source of sources.values()) source.obscure(visible)
}
export function registerReportCanvas(canvas: HTMLCanvasElement, capture: () => HTMLCanvasElement, obscure: (visible:boolean) => void) {
  sources.set(canvas, {capture, obscure})
  obscure(reporting)
  return () => { sources.delete(canvas) }
}

export async function captureReportScreen(): Promise<string> {
  // WebGL drawing buffers may be cleared between frames. Extract through Pixi instead of enabling
  // preserveDrawingBuffer for every frame (which would penalize play even when nobody reports).
  const snapshots = new Map<string, string>()
  let index = 0
  try {
  for (const [canvas, {capture}] of sources) {
      const key = String(index++)
      canvas.dataset.reportCanvas = key
      snapshots.set(key, capture().toDataURL('image/png'))
    }
    const { default: html2canvas } = await import('html2canvas')
    const result = await html2canvas(document.body, {
      width: window.innerWidth, height: window.innerHeight, scrollX: window.scrollX, scrollY: window.scrollY,
      windowWidth: window.innerWidth, windowHeight: window.innerHeight,
      scale: Math.min(1.5, 1280 / Math.max(window.innerWidth, window.innerHeight)),
      backgroundColor: '#34230f', logging: false, imageTimeout: 3000,
      ignoreElements: element => element.hasAttribute('data-report-exclude') || element.tagName === 'IFRAME' || element.classList.contains('login') || element.classList.contains('telegram-confirm'),
      onclone: doc => {
        // Cloning restarts CSS entrance animations in WebKit. Capture fully visible menus,
        // without changing the live page's animation or persisting a screenshot anywhere.
        const still = doc.createElement('style')
        still.textContent = '*,*::before,*::after{animation:none!important;transition:none!important;caret-color:transparent!important}'
        doc.head.appendChild(still)
        // html2canvas does not honour the browser's collapsed-details layout by itself.
        for (const details of doc.querySelectorAll('details')) {
          const summary = details.querySelector<HTMLElement>(':scope > summary')
          if (!details.open) for (const child of [...details.childNodes]) if (child !== summary) child.remove()
          if (summary) {
            summary.style.display = 'block'
            summary.prepend(doc.createTextNode(details.open ? '▾ ' : '▸ '))
          }
        }
        for (const canvas of doc.querySelectorAll<HTMLCanvasElement>('canvas[data-report-canvas]')) {
          const source = snapshots.get(canvas.dataset.reportCanvas!)
          if (!source) continue
          const image = doc.createElement('img')
          image.src = source
          image.style.cssText = canvas.style.cssText
          image.className = canvas.className
          image.width = canvas.clientWidth || canvas.width
          image.height = canvas.clientHeight || canvas.height
          canvas.replaceWith(image)
        }
        // Credentials and account/login forms are never part of an attachment.
        for (const input of doc.querySelectorAll<HTMLInputElement>('input[type="password"],input[type="email"],input[autocomplete="one-time-code"]')) {
          input.value = ''; input.setAttribute('value', '')
        }
      },
    })
    for (const quality of [.8, .65, .5, .35]) {
      const encoded = result.toDataURL('image/jpeg', quality).split(',')[1]
      if (encoded.length <= 460000) return encoded
    }
    throw new Error('screenshot.size')
  } finally {
    for (const canvas of sources.keys()) delete canvas.dataset.reportCanvas
    snapshots.clear()
  }
}
