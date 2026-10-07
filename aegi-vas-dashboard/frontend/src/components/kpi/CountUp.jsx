import { useEffect, useRef, useState } from 'react'
import { reducedMotion } from '../../theme/motion'

export default function CountUp({ value, format, duration = 1400 }) {
  const [shown, setShown] = useState(reducedMotion ? value : 0)
  const from = useRef(0)

  useEffect(() => {
    if (reducedMotion) { setShown(value); return }
    const start = performance.now(), base = from.current
    let raf
    const tick = (t) => {
      const p = Math.min(1, (t - start) / duration), k = 1 - Math.pow(1 - p, 4)
      setShown(base + (value - base) * k)
      if (p < 1) raf = requestAnimationFrame(tick)
      else from.current = value
    }
    raf = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(raf)
  }, [value, duration])

  return format(shown)
}
