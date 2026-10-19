import { useRef, type MouseEvent } from 'react'

/** Only gestures that start inside this popup may activate its controls. Keyboard clicks remain supported. */
export function usePopupGesture() {
  const started = useRef(false)
  return {
    onPointerDownCapture: () => { started.current = true },
    onPointerCancelCapture: () => { started.current = false },
    onClickCapture: (event: MouseEvent) => {
      if (event.detail > 0 && !started.current) { event.preventDefault(); event.stopPropagation() }
      started.current = false
    },
  }
}
