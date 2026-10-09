import { motion } from 'framer-motion'
import { reducedMotion } from '../../theme/motion'
import { fmtNum, monthLabel } from '../../utils/format'

// One percentage series by month, with every point labelled. points = [{ month, value }].
export default function TrendLine({ points, label }) {
  const W = 520, H = 220, L = 40, R = 16, T = 26, B = 28
  const values = points.map((p) => p.value)
  const lo = Math.floor(Math.min(...values) - 1), hi = Math.ceil(Math.max(...values) + 1)
  const n = points.length
  const x = (i) => (n === 1 ? (L + W - R) / 2 : L + (i * (W - L - R)) / (n - 1))
  const y = (v) => T + (1 - (v - lo) / (hi - lo || 1)) * (H - T - B)
  const d = 'M' + points.map((p, i) => `${x(i)},${y(p.value)}`).join('L')
  const every = Math.ceil(n / 12)
  return (
    <svg viewBox={`0 0 ${W} ${H}`} style={{ width: '100%', height: 'auto' }} role="img"
      aria-label={`${label}: ${points.map((p) => `${monthLabel(p.month)} ${fmtNum(p.value)}%`).join(', ')}`}>
      {[lo, (lo + hi) / 2, hi].map((v) => (
        <g key={v}>
          <line x1={L} x2={W - R} y1={y(v)} y2={y(v)} stroke="var(--line)" strokeDasharray="3 4" />
          <text x={L - 8} y={y(v) + 4} textAnchor="end">{fmtNum(v, 0)}%</text>
        </g>
      ))}
      <motion.path d={d} fill="none" stroke="var(--gold)" strokeWidth="2.2" strokeLinejoin="round"
        initial={{ pathLength: reducedMotion ? 1 : 0 }} animate={{ pathLength: 1 }} transition={{ duration: 1.4, ease: [0.2, 0.8, 0.2, 1] }} />
      {points.map((p, i) => (
        <g key={p.month}>
          <circle cx={x(i)} cy={y(p.value)} r="3.5" fill="var(--gold2)" stroke="var(--bg)" strokeWidth="1.5" />
          {n <= 12 && <text x={x(i)} y={y(p.value) - 9} textAnchor="middle" style={{ fill: 'var(--fg)' }}>{fmtNum(p.value)}</text>}
          {i % every === 0 && <text x={x(i)} y={H - 8} textAnchor="middle">{monthLabel(p.month)}</text>}
        </g>
      ))}
    </svg>
  )
}
