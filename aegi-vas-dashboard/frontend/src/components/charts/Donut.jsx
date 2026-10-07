import { motion } from 'framer-motion'
import { reducedMotion } from '../../theme/motion'
import { fmtNum } from '../../utils/format'

const COLORS = { Approved: 'var(--ok)', Rejected: 'var(--bad)', Pending: 'var(--warn)' }

export default function Donut({ outcomes }) {
  const cx = 130, cy = 112, r = 84, sw = 22, C = 2 * Math.PI * r
  let offset = 0
  const approved = outcomes.find((o) => o.outcome === 'Approved')
  return (
    <>
      <svg viewBox="0 0 260 230" style={{ width: '100%', height: 'auto', maxHeight: 260 }} role="img"
        aria-label={`Visa outcomes: ${outcomes.map((o) => `${o.outcome} ${fmtNum(o.percent)}%`).join(', ')}`}>
        <circle cx={cx} cy={cy} r={r} fill="none" stroke="var(--line)" strokeWidth={sw} />
        {outcomes.map((o, i) => {
          const len = (o.percent / 100) * C, start = offset
          offset += len
          return (
            <motion.circle key={o.outcome} cx={cx} cy={cy} r={r} fill="none" stroke={COLORS[o.outcome] || 'var(--faint)'} strokeWidth={sw}
              strokeDashoffset={-start} transform={`rotate(-90 ${cx} ${cy})`}
              initial={{ strokeDasharray: `${reducedMotion ? Math.max(0, len - 2) : 0} ${C}` }}
              animate={{ strokeDasharray: `${Math.max(0, len - 2)} ${C}` }}
              transition={{ duration: 1, delay: 0.2 + i * 0.15, ease: [0.2, 0.8, 0.2, 1] }} />
          )
        })}
        <text x={cx} y={cy - 2} textAnchor="middle" style={{ fill: 'var(--fg)', fontFamily: 'var(--display)', fontSize: 30 }}>
          {approved ? `${fmtNum(approved.percent)}%` : '–'}
        </text>
        <text x={cx} y={cy + 20} textAnchor="middle">approved</text>
      </svg>
      <div className="legend" style={{ justifyContent: 'center' }}>
        {outcomes.map((o) => <span key={o.outcome}><i style={{ background: COLORS[o.outcome] || 'var(--faint)' }} />{o.outcome} {fmtNum(o.percent)}%</span>)}
      </div>
    </>
  )
}
