import { motion } from 'framer-motion'
import { reducedMotion } from '../../theme/motion'

// Plain horizontal bars: one row per item, longest bar full width. items = [{ label, value, display, note }].
// Also the fallback for the 3D revenue view when the browser has no WebGL.
export default function BarList({ items, label }) {
  const max = Math.max(...items.map((i) => i.value), 0) || 1
  return (
    <ul className="barlist" aria-label={label}>
      {items.map((it, i) => (
        <li key={it.label}>
          <span className="bl-label">{it.label}</span>
          <span className="bl-track">
            <motion.i initial={{ width: reducedMotion ? `${(it.value / max) * 100}%` : 0 }} animate={{ width: `${(it.value / max) * 100}%` }}
              transition={{ duration: 0.8, delay: i * 0.05, ease: [0.2, 0.8, 0.2, 1] }} />
          </span>
          <span className="bl-value">{it.display}{it.note && <small> {it.note}</small>}</span>
        </li>
      ))}
    </ul>
  )
}
