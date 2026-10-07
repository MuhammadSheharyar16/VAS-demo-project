import { motion } from 'framer-motion'
import { reducedMotion } from '../../theme/motion'

export default function Sparkline({ values, width = 96, height = 30 }) {
  if (!values || values.length < 2) return null
  const mn = Math.min(...values), mx = Math.max(...values)
  const pts = values.map((v, i) => [3 + (i * (width - 6)) / (values.length - 1), height - 4 - ((v - mn) / (mx - mn || 1)) * (height - 8)])
  const d = 'M' + pts.map((p) => p.join(',')).join('L')
  const last = pts[pts.length - 1]
  return (
    <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`} aria-hidden="true">
      <path d={`${d}L${last[0]},${height}L3,${height}Z`} fill="rgba(214,178,94,.12)" />
      <motion.path d={d} fill="none" stroke="var(--gold)" strokeWidth="1.6" strokeLinecap="round"
        initial={{ pathLength: reducedMotion ? 1 : 0 }} animate={{ pathLength: 1 }} transition={{ duration: 1.4, ease: [0.2, 0.8, 0.2, 1] }} />
      <circle cx={last[0]} cy={last[1]} r="2.6" fill="var(--gold2)" />
    </svg>
  )
}
