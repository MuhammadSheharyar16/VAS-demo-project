import { motion } from 'framer-motion'
import { reducedMotion } from '../../theme/motion'
import { fmtNum } from '../../utils/format'

// Horizontal bars for turnaround days by visa type. The longest bar is highlighted; values come from the API.
export default function HBars({ items, average }) {
  const W = 320, L = 70, R = 40, rh = 44
  const top = Math.max(...items.map((i) => i.avgDays), 0)
  const max = Math.max(1, Math.ceil(top + 0.5))
  const H = 14 + items.length * rh + 16
  return (
    <svg viewBox={`0 0 ${W} ${H}`} style={{ width: '100%', height: 'auto' }} role="img" aria-label="Turnaround time by visa type">
      {items.map((it, i) => {
        const yy = 14 + i * rh, w = ((W - L - R) * it.avgDays) / max
        return (
          <g key={it.visaType}>
            <text x={L - 10} y={yy + 15} textAnchor="end" style={{ fill: 'var(--fg)' }}>{it.visaType}</text>
            <rect x={L} y={yy + 4} width={W - L - R} height="16" rx="8" fill="var(--line)" fillOpacity=".5" />
            <motion.rect x={L} y={yy + 4} height="16" rx="8" fill={it.avgDays === top ? 'var(--warn)' : 'var(--gold)'}
              initial={{ width: reducedMotion ? w : 0 }} animate={{ width: w }} transition={{ duration: 0.9, delay: i * 0.06, ease: [0.2, 0.8, 0.2, 1] }} />
            <text x={L + w + 8} y={yy + 16} style={{ fill: 'var(--fg)', fontWeight: 600 }}>{fmtNum(it.avgDays)}d</text>
          </g>
        )
      })}
      {average != null && <text x={L} y={H - 4}>Average across all types: {fmtNum(average)} days</text>}
    </svg>
  )
}
